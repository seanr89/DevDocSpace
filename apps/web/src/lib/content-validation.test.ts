import { describe, expect, it } from "vitest";
import { isValidDocPath, isValidSlug, validateSpecJson } from "./content-validation";

describe("isValidSlug", () => {
  it.each(["petstore", "v1", "1.2.0", "my_svc-2"])("accepts %s", (s) => expect(isValidSlug(s)).toBe(true));
  it.each(["", "Petstore", "../x", ".hidden", "a b", "a/b", "x".repeat(65)])("rejects %s", (s) => expect(isValidSlug(s)).toBe(false));
});

describe("isValidDocPath", () => {
  it.each(["index", "guides/auth", "a/b/c"])("accepts %s", (p) => expect(isValidDocPath(p)).toBe(true));
  it.each(["", "/index", "guides/", "a//b", "../secret", "page.md", "page.mdx", "Guides/x", "1/2/3/4/5/6/7/8/9"])("rejects %s", (p) =>
    expect(isValidDocPath(p)).toBe(false),
  );
});

describe("validateSpecJson", () => {
  it("accepts an OpenAPI document", () => {
    expect(validateSpecJson('{"openapi":"3.1.0","info":{"title":"x"}}')).toBeNull();
  });
  it("accepts a Swagger document", () => {
    expect(validateSpecJson('{"swagger":"2.0","info":{}}')).toBeNull();
  });
  it.each([
    ["", /required/],
    ["{", /not valid JSON/],
    ["[]", /JSON object/],
    ['{"info":{}}', /openapi/],
    ['{"openapi":3,"info":{}}', /openapi/],
    ['{"openapi":"3.0.0"}', /info/],
  ])("rejects %j", (input, message) => expect(validateSpecJson(input)).toMatch(message));
});
