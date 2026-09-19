export function parseFrontmatter(source: string): { content: string; data: Record<string, string> } {
  const match = /^---\r?\n([\s\S]*?)\r?\n---\r?\n?/.exec(source);
  if (!match) return { content: source, data: {} };
  const data: Record<string, string> = {};
  for (const line of match[1].split(/\r?\n/)) {
    const idx = line.indexOf(":");
    if (idx === -1) continue;
    const key = line.slice(0, idx).trim();
    const value = line.slice(idx + 1).trim().replace(/^["'](.*)["']$/, "$1");
    if (key) data[key] = value;
  }
  return { content: source.slice(match[0].length), data };
}
