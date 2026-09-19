import { describe, expect, it } from "vitest";
import { parseFrontmatter } from "./frontmatter";

describe("parseFrontmatter", () => {
  it("extracts simple key/value pairs and strips the block", () => {
    const { content, data } = parseFrontmatter('---\ntitle: "Hello"\nowner: docs\n---\n# Body');
    expect(data).toEqual({ title: "Hello", owner: "docs" });
    expect(content).toBe("# Body");
  });

  it("returns the source untouched without frontmatter", () => {
    expect(parseFrontmatter("# Just markdown")).toEqual({ content: "# Just markdown", data: {} });
  });
});
