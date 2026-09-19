"use client";

import Link from "next/link";
import { useApiData } from "@/lib/use-api-data";

export default function DocsIndexPage() {
  const { data, error, loading } = useApiData((api) => api.namespaces(), []);

  return (
    <main className="mx-auto max-w-5xl px-4 py-10">
      <h1 className="text-3xl font-semibold tracking-tight">Documentation</h1>
      {loading && <p className="mt-6 text-sm text-zinc-500">Loading…</p>}
      {error && <p className="mt-6 text-sm text-red-600">{error}</p>}
      <ul className="mt-6 grid gap-3 sm:grid-cols-2">
        {data?.map((ns) => (
          <li key={ns.slug}>
            <Link
              href={`/docs/${ns.slug}`}
              className="block rounded border border-zinc-200 dark:border-zinc-800 p-4 hover:border-zinc-400"
            >
              <div className="font-medium">{ns.title}</div>
              <div className="text-xs text-zinc-500">{ns.slug}</div>
            </Link>
          </li>
        ))}
        {data?.length === 0 && <li className="text-sm text-zinc-500">No documentation namespaces are visible to you.</li>}
      </ul>
    </main>
  );
}
