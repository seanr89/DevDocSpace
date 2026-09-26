"use client";

import { useState } from "react";
import Link from "next/link";
import { useRouter } from "next/navigation";
import { useApiData } from "@/lib/use-api-data";
import { isValidSlug } from "@/lib/content-validation";
import { AdminOnly, ErrorText, btnCls, inputCls } from "@/components/admin-ui";

export default function AdminDocsPage() {
  const router = useRouter();
  const namespaces = useApiData((api) => api.namespaces(), []);
  const [slug, setSlug] = useState("");
  const invalid = slug !== "" && !isValidSlug(slug);

  function open(e: React.FormEvent) {
    e.preventDefault();
    router.push(`/admin/docs/${encodeURIComponent(slug)}`);
  }

  return (
    <AdminOnly>
      <main className="mx-auto max-w-6xl px-4 py-10 space-y-6">
        <div>
          <Link href="/admin" className="text-sm text-zinc-500 hover:underline">
            ← Admin
          </Link>
          <h1 className="mt-2 text-3xl font-semibold tracking-tight">Documentation pages</h1>
          <p className="mt-1 text-sm text-zinc-500">
            Pick a namespace to add or edit pages. A new namespace appears for readers once it has a page; set its title and
            required role under Admin → Documentation namespaces.
          </p>
        </div>
        <ErrorText>{namespaces.error}</ErrorText>
        <ul className="divide-y divide-zinc-200 dark:divide-zinc-800 rounded border border-zinc-200 dark:border-zinc-800">
          {namespaces.data?.map((n) => (
            <li key={n.slug}>
              <Link href={`/admin/docs/${encodeURIComponent(n.slug)}`} className="flex items-center gap-3 px-4 py-2 hover:bg-zinc-50 dark:hover:bg-zinc-900">
                <span className="font-mono text-sm">{n.slug}</span>
                {n.title !== n.slug && <span className="text-sm text-zinc-500">{n.title}</span>}
              </Link>
            </li>
          ))}
          {namespaces.data?.length === 0 && <li className="px-4 py-2 text-sm text-zinc-500">No namespaces yet.</li>}
        </ul>
        <form onSubmit={open} className="flex flex-wrap items-center gap-2">
          <input aria-label="New namespace" placeholder="new-namespace" value={slug} onChange={(e) => setSlug(e.target.value.trim())} className={inputCls} />
          <button type="submit" disabled={!slug || invalid} className={btnCls}>
            New namespace
          </button>
          {invalid && <ErrorText>Use a lowercase slug (a-z, 0-9, &apos;.&apos;, &apos;_&apos;, &apos;-&apos;).</ErrorText>}
        </form>
      </main>
    </AdminOnly>
  );
}
