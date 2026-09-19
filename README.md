# DevDocSpace

A developer portal that unifies Markdown documentation with interactive OpenAPI references and a proxied "Try it out" console. See `system_design_document.md` for the design.

- `apps/web` — Next.js (App Router) frontend, Firebase Auth, `swagger-ui-react`
- `apps/api` — .NET 10 Minimal API: content, RBAC, spec registry, API-call proxy, API keys (EF Core + PostgreSQL)
- `content/` — local content store: `docs/<namespace>/**.md` and `specs/<service>/<version>/openapi.json`

## Prerequisites

Node 24, .NET 10 SDK, Docker.

## Run locally

```bash
npm run db:up                                   # Postgres on :5432

# API (http://localhost:5080) — applies migrations on start in Development
cd apps/api
dotnet run --project DevDocSpace.Api

# Web (http://localhost:3000)
cd apps/web
cp .env.example .env.local                      # fill in NEXT_PUBLIC_FIREBASE_*
npm install && npm run dev
```

Configure the API via `apps/api/DevDocSpace.Api/appsettings.Development.json` or environment variables:

- `Auth:FirebaseProjectId` — required; must match `NEXT_PUBLIC_FIREBASE_PROJECT_ID`
- `Auth:AdminEmails` — emails that become `Admin` on first sign-in
- `Auth:UseFirebaseEmulator` — accept unsigned tokens from the Firebase Auth emulator (Development only)
- `Proxy:Credentials:<key>:{Header,Value}` — upstream credentials referenced by a spec environment's `credentialKey`

After signing in as an admin, open **Admin → Sync from content store** to register the specs in `content/specs`, then set each spec's environment base URLs and required role.

## Test

```bash
cd apps/api && dotnet test
cd apps/web && npm run lint && npm run typecheck && npm test && npx playwright test
```

## Full stack with Docker

```bash
FIREBASE_PROJECT_ID=... FIREBASE_API_KEY=... FIREBASE_AUTH_DOMAIN=... FIREBASE_APP_ID=... ADMIN_EMAIL=you@example.com \
  docker compose --profile full up --build
```

## Publishing content from a service repo

Call the reusable workflow `.github/workflows/ingest-content.yml` from the service's pipeline (see the header comment in that file). It commits the docs and `openapi.json` into `content/`.
