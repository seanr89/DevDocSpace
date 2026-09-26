# System Design: Documentation & API Developer Portal

## 1. Executive Summary
This portal serves as a unified platform for engineering teams and consumers to access software documentation, system architecture references, and interactive API specifications. It provides a Markdown-driven documentation engine alongside an interactive OpenAPI/Swagger explorer that allows users to seamlessly view, test, and manage API calls against underlying services.

## 2. System Architecture & Tech Stack

The frontend is a TypeScript/React application; the backend is a .NET service that owns the database, access control, and the API proxy.

| Component | Technology / Strategy | Purpose |
| :--- | :--- | :--- |
| **Frontend UI** | **Next.js (App Router)** / React | Renders Markdown/MDX content, interactive UI components, and the `swagger-ui-react` wrapper. |
| **Backend API / Proxy** | **.NET 10 Minimal APIs** (C#) | Serves content, enforces RBAC, and handles API proxy requests (CORS avoidance, credential injection, rate limits). |
| **Database** | **PostgreSQL** with **EF Core** (Npgsql) | Stores user roles, spec registry, access policies, API keys, and analytics on API usage. |
| **Authentication** | **Firebase Auth** / JWT | Firebase ID tokens are sent as bearer tokens and validated by the backend; roles live in the database. |
| **Content Storage** | Local filesystem (`content/`) behind an `IContentStore` abstraction | Initial storage for Markdown and OpenAPI specs; AWS S3 / Azure Blob implementations are planned. |
| **CI/CD** | **GitHub Actions** | Automates the ingestion of Markdown files and `openapi.json` specs from various microservice repositories. |

**Repository layout:** `apps/web` (Next.js), `apps/api` (.NET solution: `DevDocSpace.Api`, `DevDocSpace.Data`, `DevDocSpace.Api.Tests`), `content/` (local docs and specs).

## 3. Core Functional Modules

### A. The Documentation Engine
*   **Content Ingestion:** Documentation lives as Markdown/MDX in the source code repositories of the respective services. A GitHub Action extracts these files on merge to `main` and publishes them into the portal's content store (`content/docs/<namespace>/` today; cloud storage later).
*   **Portal Authoring:** Admins can also create and edit Markdown pages in the portal. These are stored in PostgreSQL (`DocPage`) and overlay the content store: a portal page at the same namespace/path is served instead of the ingested file until an admin reverts it.
*   **Rendering:** The frontend fetches Markdown from the backend (`/api/v1/docs/...`) and server-side renders it as MDX.
*   **Global Search (deferred):** A search index (Meilisearch) over Markdown headers and OpenAPI operation IDs is planned for a later phase.

### B. Interactive API Specifications (Swagger Integration)
*   **Spec Aggregation:** Underlying services generate OpenAPI 3.0/3.1 JSON files automatically. They are published to `content/specs/<service>/<version>/openapi.json`; the backend maintains a registry (`ApiSpec` table) mapping each spec to a required role and its environment base URLs.
*   **Portal Authoring:** Admins can register a spec directly in the portal or edit an ingested one. The JSON is stored on the `ApiSpec` row and served instead of the file; reverting clears it. Portal-only specs have no content-store path.
*   **UI Rendering:** `swagger-ui-react` renders the OpenAPI specs into human-readable reference pages.
*   **Versioning:** The UI includes a dropdown to toggle between API versions (e.g., v1 vs. v2) by loading the respective spec from `/api/v1/specs/{service}/{version}`.

### C. API Console & Call Management (The "Try It Out" Feature)
Allowing users to make live API calls directly from the documentation portal requires careful handling of network and security constraints.

*   **The Proxy Layer:** When a user clicks "Execute" in the Swagger UI, the request does *not* go directly from their browser to the backend service. A `requestInterceptor` rewrites it to the portal's .NET backend at `/api/v1/proxy/{service}/{version}/{env}/{path}`.
*   **CORS Mitigation:** The proxy layer eliminates Cross-Origin Resource Sharing (CORS) errors that typically plague client-side API explorers.
*   **Auth Injection:** The proxy validates the user's Firebase ID token (or portal API key), checks the user's role against the spec's required role, strips hop-by-hop headers, injects the upstream credential configured for that service/environment, and forwards the request. Every call is logged to `ApiCallLog`.
*   **Environment Switching:** The UI provides a toggle (Sandbox, Staging, Production). The proxy resolves the base URL from the `ServiceEnvironment` table based on this selection.
*   **Limits:** Request bodies are size-capped and calls are rate-limited per user.

## 4. Database & Access Control (EF Core / PostgreSQL)

Not all documentation or endpoints should be visible to everyone. The PostgreSQL database manages Role-Based Access Control (RBAC).

*   **User Roles:** `Admin`, `InternalDeveloper`, `ExternalClient`. Users are upserted from Firebase token claims on first request with the default role `ExternalClient`; the database, not the token, is the source of truth for roles.
*   **Resource Mapping:** `ApiSpec` and `DocNamespace` tables map specific API specs or documentation namespaces to required roles.
*   **Portal Content:** `ApiSpec.Content` and `DocPage` hold admin-authored content (with who last edited it and when); only `Admin` users can write them.
*   **API Key Management:** External users can generate and manage their own API keys from a dashboard. The portal stores a SHA-256 hash of each key and returns the plaintext exactly once. API keys are accepted as an alternate authentication scheme on the proxy.

## 5. Deployment & CI/CD Workflow

1.  **Code Commit:** A developer updates a backend service and modifies the code comments or Markdown docs.
2.  **Spec Generation:** The service's build pipeline auto-generates a new `openapi.json`.
3.  **Portal Sync:** A reusable GitHub Action (`ingest-content.yml`) publishes the new spec and Markdown files into the portal's content store. An admin endpoint (`POST /api/v1/admin/specs/sync`) scans the store and upserts the spec registry.
4.  **Cache Invalidation (future):** Once content moves to cloud storage, the CDN cache is cleared so updated documentation is live without a portal redeployment.

## 6. Deferred Work

*   Global search (Meilisearch).
*   Cloud content storage (`S3ContentStore` / `BlobContentStore`) and CDN invalidation.
*   Syncing roles into Firebase custom claims.