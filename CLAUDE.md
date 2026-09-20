# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## What this is

DevDocSpace is a developer portal: Markdown docs + interactive OpenAPI references + a proxied "Try it out" console with RBAC. `system_design_document.md` is the design source of truth. Two independent toolchains live side by side; there is no shared package between them.

- `apps/web` — Next.js 16 (App Router, TypeScript, Tailwind 4), Firebase Auth client SDK, `swagger-ui-react`, `react-markdown`.
- `apps/api` — .NET 10 Minimal APIs. `DevDocSpace.Api` (host), `DevDocSpace.Data` (EF Core + Npgsql, migrations), `DevDocSpace.Api.Tests` (xUnit).
- `content/` — local content store: `docs/<namespace>/**.md|mdx`, `specs/<service>/<version>/openapi.json`.

## Commands

`make help` lists all targets. `make dev` starts Postgres + API + web together (backgrounds both, tails their logs, `Ctrl+C` or `make stop` from another terminal tears them down); `make status` shows what's running. Individual pieces: `make db-up`/`db-down`/`db-reset`/`db-psql`, `make api`/`api-watch`, `make web`. The Makefile is a thin wrapper — see it for the underlying commands, notably:

```bash
dotnet build && dotnet test                                  # apps/api
dotnet test --filter "FullyQualifiedName~ProxyTests"         # one test class (make api-test FILTER=...)
dotnet run --project DevDocSpace.Api                          # http://localhost:5080 (5000 clashes with macOS AirPlay)
dotnet ef migrations add <Name> --project DevDocSpace.Data    # local tool manifest pins dotnet-ef (make migrate applies)
```
In Development the API runs `Database.Migrate()` on startup; OpenAPI at `/openapi/v1.json`.

Web (`apps/web`):
```bash
npm run dev            # http://localhost:3000
npm run lint && npm run typecheck && npm test && npm run build
npx vitest run src/lib/api.test.ts
npx playwright test    # e2e/ smoke tests; starts the dev server if not running
```
`npm run typecheck` runs `next typegen` first — the global `LayoutProps`/`PageProps` types come from it. Next 16 conventions differ from older training data; read `apps/web/node_modules/next/dist/docs/` when unsure (e.g. `params` is a Promise, `useSearchParams` needs a `<Suspense>` boundary).

## Architecture

**Auth flow.** The browser signs in with Firebase; every API call sends the Firebase ID token as `Authorization: Bearer`. The API validates it with `JwtBearer` (issuer/audience = Firebase project id) and, in `CurrentUser` (`apps/api/DevDocSpace.Api/Auth/CurrentUser.cs`), upserts a `User` row on first request. **Roles live in the database, not the token**: `Role` enum is ordered (`ExternalClient < InternalDeveloper < Admin`) and `Role.Satisfies(required)` is the single comparison used everywhere. `Auth:AdminEmails` seeds admins on first sign-in. A second scheme, `X-Api-Key`, resolves portal-issued keys (SHA-256 hash stored, `ApiKeyHasher`); a policy scheme picks between the two per request. `Auth:UseFirebaseEmulator=true` (Development only) accepts unsigned emulator tokens. See `FIREBASE_SETUP.md` for creating a Firebase project and configuring both apps.

**Authorization** is via `RoleRequirement` policies (`Authenticated`, `Internal`, `Admin`) plus per-resource checks: docs namespaces are gated by `DocNamespace.RequiredRole` (absent row = visible to all signed-in users); specs are gated by `ApiSpec.RequiredRole` and are only visible once registered (`POST /admin/specs/sync` scans the content store). Hidden resources return 404, not 403.

**Content store.** `IContentStore` (`apps/api/DevDocSpace.Api/Content/`) abstracts docs/specs storage; `FileSystemContentStore` reads `Content:RootPath` (default resolves to the repo's `content/`). Cloud implementations are planned; keep new content access behind this interface. Path traversal is blocked in `SafeJoin`.

**Proxy ("Try it out").** `ANY /api/v1/proxy/{service}/{version}/{env}/{**path}` (`ProxyEndpoints` + `ProxyForwarder`). It resolves `ApiSpec` → `ServiceEnvironment.BaseUrl`, strips caller auth/cookies/hop-by-hop headers, injects the upstream credential named by `ServiceEnvironment.CredentialKey` from `Proxy:Credentials`, streams the response back (minus `Set-Cookie`/CORS headers), rate-limits per user, caps body size, and writes an `ApiCallLog`. On the web side, `SpecViewer` passes a `requestInterceptor` to Swagger UI that rewrites the spec's server URL to the proxy URL (`rewriteToProxy` in `apps/web/src/lib/api.ts`) and attaches the bearer token. Environment choice is the `?env=` search param on `/apis/[service]/[version]`.

**Web data flow.** All pages are client components (Firebase auth is client-side). `AuthProvider` (`src/lib/auth.tsx`) owns the Firebase user, the `/me` result, and a typed `ApiClient`; `useApiData` (`src/lib/use-api-data.ts`) is the fetch hook used by every page. `(protected)/layout.tsx` redirects unauthenticated users to `/sign-in?next=`. If Firebase env vars are missing the app renders a config error instead of crashing.

**Tests.** API integration tests use `TestAppFactory`: EF InMemory (it strips the Npgsql registration), a `FakeFirebaseHandler` swapped in for the JwtBearer handler via `X-Test-Uid`/`X-Test-Email` headers, and a temp content root (`WriteContent`). `ProxyTests` spins up a real Kestrel echo upstream on a random port.

**Ingestion.** `.github/workflows/ingest-content.yml` is a reusable `workflow_call` that service repos invoke to commit docs/specs into `content/`. Registry rows are then created by the admin sync endpoint.
