"use client";

import { use } from "react";
import { useApiData } from "@/lib/use-api-data";
import { DocTree } from "@/components/doc-tree";
import { Markdown } from "@/components/markdown";

export default function DocPage({ params }: { params: Promise<{ namespace: string; slug?: string[] }> }) {
  const { namespace, slug } = use(params);
  const path = slug?.join("/") ?? "index";

  const tree = useApiData((api) => api.docTree(namespace), [namespace]);
  const doc = useApiData((api) => api.doc(namespace, path), [namespace, path]);

  return (
    <div className="mx-auto max-w-7xl px-4 py-8 grid gap-8 md:grid-cols-[220px_1fr]">
      <aside className="md:sticky md:top-8 self-start">
        <div className="mb-2 text-sm font-semibold">{namespace}</div>
        {tree.data && <DocTree ns={namespace} entries={tree.data} current={path} />}
        {tree.error && <p className="text-xs text-red-600">{tree.error}</p>}
      </aside>
      <main className="min-w-0">
        {doc.loading && <p className="text-sm text-zinc-500">Loading…</p>}
        {doc.error && <p className="text-sm text-red-600">{doc.status === 404 ? "Page not found." : doc.error}</p>}
        {doc.data && <Markdown source={doc.data} />}
      </main>
    </div>
  );
}
