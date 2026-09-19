import { afterEach, describe, expect, it, vi } from "vitest";
import { API_URL, ApiError, createApiClient, proxyUrl, rewriteToProxy } from "./api";

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
    const api = createApiClient(async () => "tok");
    const me = await api.me();
    expect(me.role).toBe("Admin");
    const [url, init] = fetchMock.mock.calls[0] as unknown as [string, RequestInit];
    expect(url).toBe(`${API_URL}/api/v1/me`);
    expect((init.headers as Record<string, string>).Authorization).toBe("Bearer tok");
  });

  it("throws ApiError with status on failure", async () => {
    vi.stubGlobal("fetch", vi.fn(async () => new Response("", { status: 404 })));
    const api = createApiClient(async () => null);
    await expect(api.doc("ns", "missing")).rejects.toMatchObject({ status: 404 } satisfies Partial<ApiError>);
  });
});
