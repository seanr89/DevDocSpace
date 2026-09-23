import { beforeEach, describe, expect, it } from "vitest";
import { DEV_USERS, clearDevUser, devAuthEnabledFor, devAuthHeaders, loadDevUser, saveDevUser } from "./dev-auth";

describe("devAuthEnabledFor", () => {
  it("is on in development when the flag is set", () => {
    expect(devAuthEnabledFor({ nodeEnv: "development", flag: "true", firebaseApiKey: "key" })).toBe(true);
  });

  it("is on automatically in development when Firebase is not configured", () => {
    expect(devAuthEnabledFor({ nodeEnv: "development", flag: undefined, firebaseApiKey: undefined })).toBe(true);
    expect(devAuthEnabledFor({ nodeEnv: "development", flag: undefined, firebaseApiKey: "" })).toBe(true);
  });

  it("is off in development when Firebase is configured and the flag is not set", () => {
    expect(devAuthEnabledFor({ nodeEnv: "development", flag: undefined, firebaseApiKey: "key" })).toBe(false);
    expect(devAuthEnabledFor({ nodeEnv: "development", flag: "false", firebaseApiKey: "key" })).toBe(false);
  });

  it("is never on in a production build", () => {
    expect(devAuthEnabledFor({ nodeEnv: "production", flag: "true", firebaseApiKey: undefined })).toBe(false);
  });
});

describe("devAuthHeaders", () => {
  it("sends the email in the X-Dev-User header", () => {
    expect(devAuthHeaders("admin@devdocspace.local")).toEqual({ "X-Dev-User": "admin@devdocspace.local" });
  });
});

describe("DEV_USERS", () => {
  it("offers one preset per role", () => {
    expect(DEV_USERS.map((u) => u.role)).toEqual(["Admin", "InternalDeveloper", "ExternalClient"]);
    expect(DEV_USERS.every((u) => u.email.endsWith("@devdocspace.local"))).toBe(true);
  });
});

describe("persisted dev user", () => {
  beforeEach(() => localStorage.clear());

  it("round-trips through localStorage", () => {
    expect(loadDevUser()).toBeNull();
    saveDevUser("dev@devdocspace.local");
    expect(loadDevUser()).toBe("dev@devdocspace.local");
    clearDevUser();
    expect(loadDevUser()).toBeNull();
  });
});
