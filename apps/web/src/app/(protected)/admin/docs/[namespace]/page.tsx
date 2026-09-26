"use client";

import { use, useState } from "react";
import Link from "next/link";
import { useRouter } from "next/navigation";
import { useApiData } from "@/lib/use-api-data";
import { isValidDocPath } from "@/lib/content-validation";
import { AdminOnly, ErrorText, SourceBadge, btnCls, inputCls } from "@/components/admin-ui";

export default function AdminNamespacePage({ params }: { params: Promise<{ namespace: string }> }) {
  const { namespace } = use(params);
  const router = useRouter();
  const pages = useApiData((api) => api.admin.docPages(namespace), [namespace]);
  const [path, setPath] = useState("");
  const invalid = path !== "" && !isValidDocPath(path);

  function create(e: React.FormEvent) {
    e.preventDefault();
    router.push(`/admin/docs/${encodeURIComponent(namespace)}/${path}`);
  }

  return (
    <AdminOnly>
      <main className="mx-auto max-w-6xl px-4 py-10 space-y-6">
        <div>
          <Link href="/admin/docs" className="text-sm text-zinc-500 hover:underline">
            ← Documentation pages
          </Link>
          <h1 className="mt-2 text-3xl font-semibold tracking-tight font-mono">{namespace}</h1>
        </div>
        <ErrorText>{pages.error}</ErrorText>
        <table className="w-full text-sm">
          <thead className="text-left text-zinc-500">
            <tr>
              <th className="py-1">Page</th>
              <th>Source</th>
              <th>Last portal edit</th>
            </tr>
          </thead>
          <tbody>
            {pages.data?.map((p) => (
              <tr key={p.path} className="border-t border-zinc-200 dark:border-zinc-800">
                <td className="py-1.5 font-mono">
                  <Link href={`/admin/docs/${encodeURIComponent(namespace)}/${p.path}`} className="underline">
                    {p.path}
                  </Link>
                </td>
                <td>
                  <SourceBadge source={p.source} />
                </td>
                <td>{p.updatedAt ? new Date(p.updatedAt).toLocaleString() : "—"}</td>
              </tr>
            ))}
            {pages.data?.length === 0 && (
              <tr>
                <td colSpan={3} className="py-2 text-zinc-500">
                  No pages yet. Start with <span className="font-mono">index</span>.
                </td>
              </tr>
            )}
          </tbody>
        </table>
        <form onSubmit={create} className="flex flex-wrap items-center gap-2">
          <input aria-label="New page path" placeholder="guides/getting-started" value={path} onChange={(e) => setPath(e.target.value.trim())} className={inputCls} />
          <button type="submit" disabled={!path || invalid} className={btnCls}>
            New page
          </button>
          {invalid && <ErrorText>Use lowercase slugs separated by &apos;/&apos;, without a file extension.</ErrorText>}
        </form>
      </main>
    </AdminOnly>
  );
}
