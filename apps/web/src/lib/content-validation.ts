// Client-side mirror of the API's ContentValidation rules, for instant feedback in the admin editors.

const SLUG = /^[a-z0-9][a-z0-9._-]{0,63}$/;
const MAX_DOC_SEGMENTS = 8;

export function isValidSlug(value: string): boolean {
  return SLUG.test(value);
}

// A doc page path relative to its namespace, without extension, e.g. "guides/auth" or "index".
export function isValidDocPath(path: string): boolean {
  if (!path) return false;
  const segments = path.split("/");
  return segments.length <= MAX_DOC_SEGMENTS && segments.every(isValidSlug) && !/\.mdx?$/.test(path);
}

// Returns an error message, or null when the text looks like an OpenAPI/Swagger JSON document.
export function validateSpecJson(text: string): string | null {
  if (!text.trim()) return "Spec content is required.";
  let root: unknown;
  try {
    root = JSON.parse(text);
  } catch (e) {
    return `Spec is not valid JSON: ${e instanceof Error ? e.message : String(e)}`;
  }
  if (typeof root !== "object" || root === null || Array.isArray(root)) return "Spec must be a JSON object.";
  const doc = root as Record<string, unknown>;
  if (typeof (doc.openapi ?? doc.swagger) !== "string") return 'Spec must have a string "openapi" (or "swagger") field.';
  if (typeof doc.info !== "object" || doc.info === null || Array.isArray(doc.info)) return 'Spec must have an "info" object.';
  return null;
}
