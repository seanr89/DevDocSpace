# DevDocSpace

A developer portal that unifies Markdown documentation with interactive OpenAPI references and a proxied "Try it out" console. See `system_design_document.md` for the design.

- `apps/web` — Next.js (App Router) frontend, Firebase Auth, `swagger-ui-react`
- `apps/api` — .NET 10 Minimal API: content, RBAC, spec registry, API-call proxy, API keys (EF Core + PostgreSQL)
- `content/` — local content store: `docs/<namespace>/**.md` and `specs/<service>/<version>/openapi.json`

## Prerequisites

Node 24, .NET 10 SDK, Docker.

## Run locally

The `Makefile` wraps the db/api/web dev loop. Run `make help` for the full list.

```bash
make install     # dotnet restore + tool restore, npm install, creates apps/web/.env.local
                  # fill in NEXT_PUBLIC_FIREBASE_* in apps/web/.env.local before signing in
                  # see FIREBASE_SETUP.md for how to get those values

make dev         # starts Postgres, then runs api + web in this terminal (Ctrl+C stops all)
                  # API:  http://localhost:5080
                  # Web:  http://localhost:3000
```

`make dev` runs both processes in the background and tails their logs; from another terminal, `make status` shows what's running and `make stop` shuts down the api/web processes (the database keeps running — use `make db-down` to stop it too). If a `make dev` session gets orphaned (e.g. the terminal was closed), `make stop` still cleans it up by PID file and by process name.

Individual pieces, each in the foreground in their own terminal:

```bash
make db-up        # Postgres on :5432 (make db-down / make db-reset / make db-psql / make db-logs)
make api          # API on :5080, applies EF Core migrations on start in Development
make api-watch    # API with hot reload
make web          # web app on :3000
```

Configure the API via `apps/api/DevDocSpace.Api/appsettings.Development.json` or environment variables:

- `Auth:FirebaseProjectId` — required; must match `NEXT_PUBLIC_FIREBASE_PROJECT_ID`
- `Auth:AdminEmails` — emails that become `Admin` on first sign-in
- `Auth:UseFirebaseEmulator` — accept unsigned tokens from the Firebase Auth emulator (Development only)
- `Proxy:Credentials:<key>:{Header,Value}` — upstream credentials referenced by a spec environment's `credentialKey`

See **[FIREBASE_SETUP.md](FIREBASE_SETUP.md)** for step-by-step instructions on creating a Firebase project, enabling sign-in providers, and wiring the config into both apps (including the auth emulator and Docker Compose).

After signing in as an admin, open **Admin → Sync from content store** to register the specs in `content/specs`, then set each spec's environment base URLs and required role.

## Test

```bash
make api-test                                # or: make api-test FILTER="FullyQualifiedName~ProxyTests"
make web-lint web-typecheck web-test web-e2e
```

## Full stack with Docker

```bash
FIREBASE_PROJECT_ID=... FIREBASE_API_KEY=... FIREBASE_AUTH_DOMAIN=... FIREBASE_APP_ID=... ADMIN_EMAIL=you@example.com \
  docker compose --profile full up --build
```

## Publishing content from a service repo

Call the reusable workflow `.github/workflows/ingest-content.yml` from the service's pipeline (see the header comment in that file). It commits the docs and `openapi.json` into `content/`.
