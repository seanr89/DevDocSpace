# Firebase setup

DevDocSpace uses Firebase only for authentication. The web app signs users in with the Firebase Auth client SDK and sends the resulting ID token to the API as `Authorization: Bearer <token>`; the API verifies that token against Google's public keys — it never calls the Firebase Admin SDK or needs a service account. Roles (`ExternalClient`/`InternalDeveloper`/`Admin`) live in the API's own database, not in Firebase.

> **Don't need this yet?** The local stack works without Firebase: leave `NEXT_PUBLIC_FIREBASE_*` unset and the sign-in page offers preset dev users instead (see "Dev auth" in the README). Come back here when you want real sign-in.

This guide walks through creating a Firebase project and wiring its config into both apps. Do this once per environment (local dev, staging, prod each get their own Firebase project or at least their own Web App).

## 1. Create a Firebase project

1. Go to the [Firebase console](https://console.firebase.google.com/) and click **Add project**.
2. Name it (e.g. `devdocspace-dev`) and finish the wizard. Google Analytics is not used — you can skip it.

## 2. Register a Web App

1. In the project's **Project settings** (gear icon) → **General**, under **Your apps**, click the **Web** (`</>`) icon.
2. Give it a nickname (e.g. `devdocspace-web`) — Firebase Hosting is not needed, leave that unchecked.
3. Copy the `firebaseConfig` values shown: `apiKey`, `authDomain`, `projectId`, `appId`. You'll paste these into `apps/web/.env.local` in step 4.

## 3. Enable sign-in providers

DevDocSpace's sign-in page (`apps/web/src/app/sign-in/page.tsx`) offers Google and Email/Password.

1. In the console, go to **Build → Authentication → Sign-in method**.
2. Enable **Email/Password**.
3. Enable **Google**, set a support email when prompted.

If you only need one of these for local testing, enabling just one is fine — the sign-in page still renders both buttons, but the disabled provider will fail on use.

## 4. Configure the web app

`make install` copies `apps/web/.env.example` to `apps/web/.env.local` if it doesn't already exist. Fill in the values from step 2:

```bash
NEXT_PUBLIC_API_URL=http://localhost:5080
NEXT_PUBLIC_FIREBASE_API_KEY=<apiKey>
NEXT_PUBLIC_FIREBASE_AUTH_DOMAIN=<authDomain>       # <project-id>.firebaseapp.com
NEXT_PUBLIC_FIREBASE_PROJECT_ID=<projectId>
NEXT_PUBLIC_FIREBASE_APP_ID=<appId>
```

Restart `npm run dev` (or `make web`) after editing `.env.local` — Next.js only reads it at startup. If any `NEXT_PUBLIC_FIREBASE_*` value is missing, `apps/web/src/lib/auth.tsx` surfaces a config error on the sign-in page instead of crashing.

## 5. Configure the API

The API only needs the Firebase **project ID** — it validates ID tokens itself via `https://securetoken.google.com/<project-id>` as issuer/audience (see `apps/api/DevDocSpace.Api/Program.cs`). Set it in `apps/api/DevDocSpace.Api/appsettings.Development.json` (create it if it doesn't exist — it's gitignored) or as an environment variable:

```json
{
  "Auth": {
    "FirebaseProjectId": "<projectId>",
    "AdminEmails": ["you@example.com"]
  }
}
```

or

```bash
Auth__FirebaseProjectId=<projectId>
Auth__AdminEmails__0=you@example.com
```

`FirebaseProjectId` **must match** `NEXT_PUBLIC_FIREBASE_PROJECT_ID` on the web side, or token validation fails with 401s. `AdminEmails` lists addresses that get the `Admin` role the first time they sign in (`CurrentUser.cs` upserts the `User` row and checks this list on that first request only — changing it later doesn't retroactively promote/demote existing users; use the admin UI or the database for that).

## 6. Sign in and confirm

1. Start the stack (`make dev`, or `make db-up` + `make api` + `make web`).
2. Visit `http://localhost:3000`, get redirected to `/sign-in`.
3. Sign in with Google or an email/password account you create via the button flow.
4. If your email is in `Auth:AdminEmails`, you should land with admin access — check **Admin → Sync from content store** is visible in the nav.

## Optional: Firebase Auth emulator

To develop without hitting real Firebase (no network, no real accounts):

```bash
firebase emulators:start --only auth
```

Then set, on the web side (`apps/web/.env.local`):

```bash
NEXT_PUBLIC_FIREBASE_AUTH_EMULATOR_URL=http://127.0.0.1:9099
```

and on the API side:

```bash
Auth__UseFirebaseEmulator=true
```

`Auth:UseFirebaseEmulator` disables signature validation so unsigned emulator tokens are accepted — the API refuses to start with it set outside the `Development` environment (see `Program.cs`). `NEXT_PUBLIC_FIREBASE_PROJECT_ID` still needs to be set to *some* project id (the emulator doesn't require a real one) and must match `Auth:FirebaseProjectId` as usual.

## Docker Compose (`--profile full`)

The Docker Compose stack takes Firebase config as top-level environment variables and threads them into both containers:

```bash
FIREBASE_PROJECT_ID=<projectId> \
FIREBASE_API_KEY=<apiKey> \
FIREBASE_AUTH_DOMAIN=<authDomain> \
FIREBASE_APP_ID=<appId> \
ADMIN_EMAIL=you@example.com \
  docker compose --profile full up --build
```

See `docker-compose.yml` for how these map to `Auth__FirebaseProjectId` and `NEXT_PUBLIC_FIREBASE_*`.

## Production notes

- Register a separate Web App (or a separate Firebase project entirely) per environment so dev/staging/prod tokens can't cross over.
- In **Authentication → Settings → Authorized domains**, add your deployed web app's domain(s) — Firebase rejects sign-in from origins not on this list.
- The `NEXT_PUBLIC_FIREBASE_API_KEY` is not a secret (it identifies the Firebase project to Google's client SDK; access is still governed by your sign-in providers and the API's own RBAC) — restricting it via Google Cloud API key restrictions is good practice but not required for the app to function correctly.
- Keep `Auth:UseFirebaseEmulator` and `Auth:UseDevAuth` unset (or `false`) outside Development; the API enforces both at startup.

## Troubleshooting

| Symptom | Likely cause |
|---|---|
| Sign-in page shows "Firebase is not configured" | A `NEXT_PUBLIC_FIREBASE_*` var is missing in `.env.local` (in a production build; `next dev` falls back to dev auth instead); restart after editing it. |
| Sign-in page shows "Local development sign-in" instead of Google/email | `NEXT_PUBLIC_FIREBASE_API_KEY` is empty or `NEXT_PUBLIC_DEV_AUTH=true`; restart `npm run dev` after editing `.env.local`. |
| Sign-in succeeds but every API call returns 401 | `Auth:FirebaseProjectId` on the API doesn't match `NEXT_PUBLIC_FIREBASE_PROJECT_ID` on the web app. |
| Google sign-in popup errors with `auth/unauthorized-domain` | Add your dev/deployed origin under **Authentication → Settings → Authorized domains**. |
| Signed in but stuck without admin access | Your email isn't in `Auth:AdminEmails`, or it was added after you already signed in once — see step 5. |
| Emulator tokens rejected with 401 | `Auth:UseFirebaseEmulator` isn't set to `true` on the API, or the API isn't running in `Development`. |
