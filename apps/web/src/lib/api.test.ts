import { afterEach, describe, expect, it, vi } from "vitest";
import { API_URL, ApiError, bearerHeaders, createApiClient, proxyUrl, rewriteToProxy } from "./api";

describe("rewriteToProxy", () => {
  it("replaces a matching spec server URL with the proxy route", () => {
    const url = rewriteToProxy("https://petstore.example.com/v1/pets/42?limit=1", ["https://petstore.example.com/v1"], "petstore", "v1", "Sandbox");
    expect(url).toBe(`${API_URL}/api/v1/proxy/petstore/v1/sandbox/pets/42?limit=1`);
  });

  it("falls back to path + query when no server matches", () => {
    const url = rewriteToProxy("https://elsewhere.test/api/pets?x=1", ["https://petstore.example.com/v1"], "petstore", "v1", "Production");
    expect(url).toBe(`${API_URL}/api/v1/proxy/petstore/v1/production/api/pets?x=1`);
  });

  it("encodes service and version segments", () => {
    expect(proxyUrl("my svc", "v/2", "Staging", "/x")).toBe(`${API_URL}/api/v1/proxy/my%20svc/v%2F2/staging/x`);
  });
});

describe("createApiClient", () => {
  afterEach(() => vi.unstubAllGlobals());

  it("attaches the bearer token and parses JSON", async () => {
    const fetchMock = vi.fn(async () =>
      new Response(JSON.stringify({ id: "1", email: "a@b.c", role: "Admin" }), { headers: { "content-type": "application/json" } }),
    );
    vi.stubGlobal("fetch", fetchMock);
    const api = createApiClient(bearerHeaders(async () => "tok"));
    const me = await api.me();
    expect(me.role).toBe("Admin");
    const [url, init] = fetchMock.mock.calls[0] as unknown as [string, RequestInit];
    expect(url).toBe(`${API_URL}/api/v1/me`);
    expect((init.headers as Record<string, string>).Authorization).toBe("Bearer tok");
  });

  it("sends whatever headers the auth provider returns", async () => {
    const fetchMock = vi.fn(async () => new Response("[]", { headers: { "content-type": "application/json" } }));
    vi.stubGlobal("fetch", fetchMock);
    const api = createApiClient(async () => ({ "X-Dev-User": "dev@devdocspace.local" }));
    await api.specs();
    const [, init] = fetchMock.mock.calls[0] as unknown as [string, RequestInit];
    expect(init.headers).toEqual({ "X-Dev-User": "dev@devdocspace.local" });
  });

  it("throws ApiError with status on failure", async () => {
    vi.stubGlobal("fetch", vi.fn(async () => new Response("", { status: 404 })));
    const api = createApiClient(async () => ({}));
    await expect(api.doc("ns", "missing")).rejects.toMatchObject({ status: 404 } satisfies Partial<ApiError>);
  });
});

describe("admin content client", () => {
  afterEach(() => vi.unstubAllGlobals());

  function stub(body = "", init: ResponseInit = { status: 204 }) {
    const fetchMock = vi.fn(async () => new Response(init.status === 204 ? null : body, init));
    vi.stubGlobal("fetch", fetchMock);
    return () => fetchMock.mock.calls.map((c) => {
      const [url, req] = c as unknown as [string, RequestInit];
      return { url, method: req.method, body: req.body ? JSON.parse(req.body as string) : undefined };
    });
  }

  it("creates a spec", async () => {
    const calls = stub("", { status: 201 });
    await createApiClient(async () => ({})).admin.createSpec({ service: "demo", version: "v1", requiredRole: "Admin", content: "{}" });
    expect(calls()).toEqual([
      { url: `${API_URL}/api/v1/admin/specs`, method: "POST", body: { service: "demo", version: "v1", requiredRole: "Admin", content: "{}" } },
    ]);
  });

  it("updates and reverts spec content with encoded segments", async () => {
    const calls = stub();
    const api = createApiClient(async () => ({}));
    await api.admin.updateSpecContent("my svc", "v1", "{}");
    await api.admin.revertSpecContent("my svc", "v1");
    expect(calls()).toEqual([
      { url: `${API_URL}/api/v1/admin/specs/my%20svc/v1/content`, method: "PUT", body: { content: "{}" } },
      { url: `${API_URL}/api/v1/admin/specs/my%20svc/v1/content`, method: "DELETE", body: undefined },
    ]);
  });

  it("saves and deletes doc pages, encoding each path segment", async () => {
    const calls = stub();
    const api = createApiClient(async () => ({}));
    await api.admin.saveDocPage("guides", "auth/my page", "# Hi");
    await api.admin.deleteDocPage("guides", "auth/my page");
    expect(calls()).toEqual([
      { url: `${API_URL}/api/v1/admin/docs/guides/pages/auth/my%20page`, method: "PUT", body: { markdown: "# Hi" } },
      { url: `${API_URL}/api/v1/admin/docs/guides/pages/auth/my%20page`, method: "DELETE", body: undefined },
    ]);
  });
});
