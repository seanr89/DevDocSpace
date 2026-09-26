"use client";

import { use, useState } from "react";
import Link from "next/link";
import { useRouter } from "next/navigation";
import { useAuth } from "@/lib/auth";
import { useApiData } from "@/lib/use-api-data";
import type { ContentSource } from "@/lib/api";
import { isValidDocPath, isValidSlug } from "@/lib/content-validation";
import { AdminOnly, ErrorText, SourceBadge, btnCls, errorMessage, secondaryBtnCls, textareaCls } from "@/components/admin-ui";
import { Markdown } from "@/components/markdown";

export default function EditDocPage({ params }: { params: Promise<{ namespace: string; slug: string[] }> }) {
  const { namespace, slug } = use(params);
  const path = slug.join("/");
  const pages = useApiData((api) => api.admin.docPages(namespace), [namespace]);
  const doc = useApiData((api) => api.doc(namespace, path), [namespace, path]);
  const [notice, setNotice] = useState<string | null>(null);

  const source = pages.data?.find((p) => p.path === path)?.source ?? null;
  const isNew = doc.status === 404;
  const initial = isNew ? "" : doc.data;
  const validKey = isValidSlug(namespace) && isValidDocPath(path);

  function reload(message: string) {
    setNotice(message);
    pages.reload();
    doc.reload();
  }

  return (
    <AdminOnly>
      <main className="mx-auto max-w-7xl px-4 py-10 space-y-6">
        <div>
          <Link href={`/admin/docs/${encodeURIComponent(namespace)}`} className="text-sm text-zinc-500 hover:underline">
            ← {namespace}
          </Link>
          <div className="mt-2 flex flex-wrap items-center gap-3">
            <h1 className="text-2xl font-semibold tracking-tight font-mono">
              {namespace}/{path}
            </h1>
            {source ? <SourceBadge source={source} /> : isNew && <span className="text-sm text-zinc-500">new page</span>}
            {!isNew && (
              <Link href={`/docs/${encodeURIComponent(namespace)}/${path}`} className="ml-auto text-sm underline">
                View page
              </Link>
            )}
          </div>
        </div>
        {!validKey && <ErrorText>This namespace or page path can&apos;t be edited in the portal.</ErrorText>}
        {doc.loading && <p className="text-sm text-zinc-500">Loading…</p>}
        <ErrorText>{isNew ? null : doc.error}</ErrorText>
        {validKey && initial !== null && (
          // Keyed on the served content so a save or revert re-initialises the editor from the server.
          <DocForm
            key={initial}
            namespace={namespace}
            path={path}
            initial={initial}
            source={source}
            onChanged={reload}
            onEdit={() => setNotice(null)}
          />
        )}
        {notice && <p className="text-sm text-zinc-500">{notice}</p>}
      </main>
    </AdminOnly>
  );
}

function DocForm(props: {
  namespace: string;
  path: string;
  initial: string;
  source: ContentSource | null;
  onChanged: (message: string) => void;
  onEdit: () => void;
}) {
  const { api } = useAuth();
  const router = useRouter();
  const [value, setValue] = useState(props.initial);
  const [error, setError] = useState<string | null>(null);
  const dirty = value !== props.initial;

  async function save() {
    setError(null);
    try {
      await api.admin.saveDocPage(props.namespace, props.path, value);
      props.onChanged("Saved.");
    } catch (e) {
      setError(errorMessage(e));
    }
  }

  async function remove() {
    const reverting = props.source === "overridden";
    const prompt = reverting ? "Discard the portal edits and serve the ingested file again?" : "Delete this page?";
    if (!confirm(prompt)) return;
    setError(null);
    try {
      await api.admin.deleteDocPage(props.namespace, props.path);
      if (reverting) props.onChanged("Reverted to the ingested file.");
      else router.push(`/admin/docs/${encodeURIComponent(props.namespace)}`);
    } catch (e) {
      setError(errorMessage(e));
    }
  }

  return (
    <div className="space-y-3">
      <div className="grid gap-4 lg:grid-cols-2">
        <textarea
          aria-label="Markdown"
          spellCheck={false}
          value={value}
          onChange={(e) => {
            setValue(e.target.value);
            props.onEdit();
          }}
          placeholder={"---\ntitle: Page title\n---\n\n# Heading\n\nWrite Markdown here."}
          className={`${textareaCls} h-[65vh]`}
        />
        <div className="h-[65vh] overflow-auto rounded border border-zinc-200 dark:border-zinc-800 p-4">
          {value.trim() ? <Markdown source={value} /> : <p className="text-sm text-zinc-500">Preview appears here.</p>}
        </div>
      </div>
      <div className="flex items-center gap-3">
        <button onClick={save} disabled={!dirty || !value.trim()} className={btnCls}>
          Save
        </button>
        {props.source === "overridden" && (
          <button onClick={remove} className={secondaryBtnCls}>
            Revert to ingested
          </button>
        )}
        {props.source === "managed" && (
          <button onClick={remove} className={secondaryBtnCls}>
            Delete page
          </button>
        )}
        <ErrorText>{error}</ErrorText>
      </div>
    </div>
  );
}
