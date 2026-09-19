"use client";

import ReactMarkdown from "react-markdown";
import remarkGfm from "remark-gfm";
import rehypeHighlight from "rehype-highlight";
import { parseFrontmatter } from "@/lib/frontmatter";
import "highlight.js/styles/github-dark.css";

export function Markdown({ source }: { source: string }) {
  const { content, data } = parseFrontmatter(source);
  const title = data.title ?? null;
  return (
    <article className="prose prose-zinc dark:prose-invert max-w-none">
      {title && !/^#\s/m.test(content) && <h1>{title}</h1>}
      <ReactMarkdown remarkPlugins={[remarkGfm]} rehypePlugins={[rehypeHighlight]}>
        {content}
      </ReactMarkdown>
    </article>
  );
}
