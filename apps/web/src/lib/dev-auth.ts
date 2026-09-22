import type { Role } from "./api";

// Local-development stand-in for Firebase sign-in. The API (Auth:UseDevAuth, Development only)
// trusts an `X-Dev-User: <email>` header; this module decides when the web app uses it and
// remembers which preset user was picked.

export const DEV_USER_HEADER = "X-Dev-User";
const STORAGE_KEY = "devdocspace.devUser";

export type DevUser = { email: string; role: Role; label: string };

// Roles for these presets are seeded by Auth:AdminEmails / Auth:DevUsers in the API's
// appsettings.Development.json; keep the two lists in sync.
export const DEV_USERS: DevUser[] = [
  { email: "admin@devdocspace.local", role: "Admin", label: "Admin" },
  { email: "dev@devdocspace.local", role: "InternalDeveloper", label: "Internal developer" },
  { email: "client@devdocspace.local", role: "ExternalClient", label: "External client" },
];

export function devAuthEnabledFor(env: { nodeEnv?: string; flag?: string; firebaseApiKey?: string }): boolean {
  if (env.nodeEnv !== "development") return false;
  if (env.flag !== undefined) return env.flag === "true";
  return !env.firebaseApiKey;
}

// Literal process.env.* accesses so Next.js can inline them at build time.
export function isDevAuthEnabled(): boolean {
  return devAuthEnabledFor({
    nodeEnv: process.env.NODE_ENV,
    flag: process.env.NEXT_PUBLIC_DEV_AUTH,
    firebaseApiKey: process.env.NEXT_PUBLIC_FIREBASE_API_KEY,
  });
}

export function devAuthHeaders(email: string): Record<string, string> {
  return { [DEV_USER_HEADER]: email };
}

export function loadDevUser(): string | null {
  try {
    return localStorage.getItem(STORAGE_KEY);
  } catch {
    return null;
  }
}

export function saveDevUser(email: string): void {
  try {
    localStorage.setItem(STORAGE_KEY, email);
  } catch {}
}

export function clearDevUser(): void {
  try {
    localStorage.removeItem(STORAGE_KEY);
  } catch {}
}
