export const API_URL = (process.env.NEXT_PUBLIC_API_URL ?? "http://localhost:5080").replace(/\/$/, "");

export type Role = "ExternalClient" | "InternalDeveloper" | "Admin";
export type ApiEnvironment = "Sandbox" | "Staging" | "Production";
export const ENVIRONMENTS: ApiEnvironment[] = ["Sandbox", "Staging", "Production"];

export type Me = { id: string; email: string; role: Role };
export type NamespaceSummary = { slug: string; title: string };
export type DocEntry = { path: string; title: string; isDirectory: boolean; children: DocEntry[] };
export type SpecSummary = { id: string; service: string; version: string; requiredRole: Role; environments: ApiEnvironment[] };
export type ApiKeySummary = { id: string; name: string; prefix: string; createdAt: string; revokedAt: string | null };
export type CreatedApiKey = ApiKeySummary & { key: string };
export type CallLog = {
  id: number; service: string; version: string; environment: ApiEnvironment;
  method: string; path: string; statusCode: number; durationMs: number; at: string;
};
export type AdminSpec = {
  id: string; service: string; version: string; path: string; requiredRole: Role;
  environments: { name: ApiEnvironment; baseUrl: string; credentialKey: string | null }[];
};
export type AdminNamespace = { id: string; slug: string; title: string | null; requiredRole: Role };
export type AdminUser = { id: string; email: string; role: Role; createdAt: string };

export class ApiError extends Error {
  constructor(public status: number, message: string) {
    super(message);
  }
}

export type TokenProvider = () => Promise<string | null>;

export function proxyUrl(service: string, version: string, env: ApiEnvironment, path: string): string {
  return `${API_URL}/api/v1/proxy/${encodeURIComponent(service)}/${encodeURIComponent(version)}/${env.toLowerCase()}/${path.replace(/^\/+/, "")}`;
}

// Rewrites a Swagger UI request (aimed at the spec's own server URL) to go through the portal proxy.
export function rewriteToProxy(originalUrl: string, serverUrls: string[], service: string, version: string, env: ApiEnvironment): string {
  const matched = serverUrls.map((s) => s.replace(/\/$/, "")).find((s) => originalUrl.startsWith(s));
  const remainder = matched ? originalUrl.slice(matched.length) : new URL(originalUrl).pathname + new URL(originalUrl).search;
  return proxyUrl(service, version, env, remainder);
}

export function createApiClient(getToken: TokenProvider) {
  async function request<T>(method: string, path: string, body?: unknown): Promise<T> {
    const token = await getToken();
    const headers: Record<string, string> = {};
    if (token) headers.Authorization = `Bearer ${token}`;
    if (body !== undefined) headers["Content-Type"] = "application/json";

    const res = await fetch(`${API_URL}/api/v1${path}`, {
      method,
      headers,
      body: body === undefined ? undefined : JSON.stringify(body),
    });
    if (!res.ok) throw new ApiError(res.status, (await res.text()) || `HTTP ${res.status}`);
    if (res.status === 204) return undefined as T;
    const contentType = res.headers.get("content-type") ?? "";
    return (contentType.includes("application/json") ? await res.json() : await res.text()) as T;
  }

  return {
    me: () => request<Me>("GET", "/me"),
    namespaces: () => request<NamespaceSummary[]>("GET", "/docs/namespaces"),
    docTree: (ns: string) => request<DocEntry[]>("GET", `/docs/${encodeURIComponent(ns)}/tree`),
    doc: (ns: string, path: string) => request<string>("GET", `/docs/${encodeURIComponent(ns)}/${path}`),
    specs: () => request<SpecSummary[]>("GET", "/specs"),
    spec: (service: string, version: string) =>
      request<Record<string, unknown>>("GET", `/specs/${encodeURIComponent(service)}/${encodeURIComponent(version)}`),
    apiKeys: () => request<ApiKeySummary[]>("GET", "/apikeys"),
    createApiKey: (name: string) => request<CreatedApiKey>("POST", "/apikeys", { name }),
    revokeApiKey: (id: string) => request<void>("DELETE", `/apikeys/${id}`),
    calls: (take = 50) => request<CallLog[]>("GET", `/apikeys/calls?take=${take}`),
    admin: {
      specs: () => request<AdminSpec[]>("GET", "/admin/specs"),
      syncSpecs: () => request<{ discovered: number; added: number }>("POST", "/admin/specs/sync"),
      updateSpec: (service: string, version: string, body: { requiredRole: Role; environments: AdminSpec["environments"] }) =>
        request<void>("PUT", `/admin/specs/${encodeURIComponent(service)}/${encodeURIComponent(version)}`, body),
      namespaces: () => request<AdminNamespace[]>("GET", "/admin/namespaces"),
      updateNamespace: (slug: string, body: { title: string | null; requiredRole: Role }) =>
        request<void>("PUT", `/admin/namespaces/${encodeURIComponent(slug)}`, body),
      users: () => request<AdminUser[]>("GET", "/admin/users"),
      updateUserRole: (id: string, role: Role) => request<void>("PUT", `/admin/users/${id}/role`, { role }),
    },
  };
}

export type ApiClient = ReturnType<typeof createApiClient>;
