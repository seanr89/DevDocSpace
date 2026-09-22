import { act, cleanup, render, screen } from "@testing-library/react";
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { AuthProvider, useAuth } from "./auth";

function Probe() {
  const { loading, devAuth, user, me, signInAsDevUser, signOut } = useAuth();
  return (
    <div>
      <span data-testid="state">{loading ? "loading" : user ? `in:${user.email}` : "out"}</span>
      <span data-testid="dev">{String(devAuth)}</span>
      <span data-testid="role">{me?.role ?? "-"}</span>
      <button onClick={() => signInAsDevUser("dev@devdocspace.local")}>sign in</button>
      <button onClick={() => signOut()}>sign out</button>
    </div>
  );
}

describe("AuthProvider in dev-auth mode", () => {
  const fetchMock = vi.fn(async (_url: string, init?: RequestInit) => {
    const email = (init?.headers as Record<string, string>)["X-Dev-User"];
    return new Response(JSON.stringify({ id: "1", email, role: "InternalDeveloper" }), {
      headers: { "content-type": "application/json" },
    });
  });

  beforeEach(() => {
    vi.stubEnv("NODE_ENV", "development");
    vi.stubEnv("NEXT_PUBLIC_FIREBASE_API_KEY", "");
    vi.stubGlobal("fetch", fetchMock);
    localStorage.clear();
    fetchMock.mockClear();
  });
  afterEach(() => {
    cleanup();
    vi.unstubAllEnvs();
    vi.unstubAllGlobals();
  });

  it("starts signed out without touching Firebase", async () => {
    render(<AuthProvider><Probe /></AuthProvider>);
    expect(await screen.findByText("out")).toBeDefined();
    expect(screen.getByTestId("dev").textContent).toBe("true");
    expect(fetchMock).not.toHaveBeenCalled();
  });

  it("signs in as a dev user, loads /me with the dev header, and persists the choice", async () => {
    render(<AuthProvider><Probe /></AuthProvider>);
    await screen.findByText("out");
    await act(async () => screen.getByText("sign in").click());

    expect(await screen.findByText("in:dev@devdocspace.local")).toBeDefined();
    expect(await screen.findByText("InternalDeveloper")).toBeDefined();
    const [, init] = fetchMock.mock.calls[0] as unknown as [string, RequestInit];
    expect((init.headers as Record<string, string>)["X-Dev-User"]).toBe("dev@devdocspace.local");
    expect(localStorage.getItem("devdocspace.devUser")).toBe("dev@devdocspace.local");
  });

  it("restores a persisted dev user on load", async () => {
    localStorage.setItem("devdocspace.devUser", "dev@devdocspace.local");
    render(<AuthProvider><Probe /></AuthProvider>);
    expect(await screen.findByText("in:dev@devdocspace.local")).toBeDefined();
  });

  it("signs out and forgets the dev user", async () => {
    localStorage.setItem("devdocspace.devUser", "dev@devdocspace.local");
    render(<AuthProvider><Probe /></AuthProvider>);
    await screen.findByText("in:dev@devdocspace.local");
    await act(async () => screen.getByText("sign out").click());

    expect(await screen.findByText("out")).toBeDefined();
    expect(localStorage.getItem("devdocspace.devUser")).toBeNull();
  });
});
