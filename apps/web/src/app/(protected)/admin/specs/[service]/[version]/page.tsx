"use client";

import { use, useState } from "react";
import Link from "next/link";
import { useAuth } from "@/lib/auth";
import { useApiData } from "@/lib/use-api-data";
import { validateSpecJson } from "@/lib/content-validation";
import { AdminOnly, ErrorText, SourceBadge, btnCls, errorMessage, secondaryBtnCls } from "@/components/admin-ui";
import { SpecJsonEditor } from "@/components/spec-json-editor";

export default function EditSpecPage({ params }: { params: Promise<{ service: string; version: string }> }) {
  const { service, version } = use(params);
  const specs = useApiData((api) => api.admin.specs(), []);
  const content = useApiData((api) => api.admin.specContent(service, version), [service, version]);
  const spec = specs.data?.find((s) => s.service === service && s.version === version);
  const initial = content.data ? JSON.stringify(content.data, null, 2) : null;
  const [notice, setNotice] = useState<string | null>(null);
  const reload = (message: string) => {
    setNotice(message);
    specs.reload();
    content.reload();
  };

  return (
    <AdminOnly>
      <main className="mx-auto max-w-6xl px-4 py-10 space-y-6">
        <div>
          <Link href="/admin" className="text-sm text-zinc-500 hover:underline">
            ← Admin
          </Link>
          <div className="mt-2 flex flex-wrap items-center gap-3">
            <h1 className="text-3xl font-semibold tracking-tight">
              {service} <span className="text-zinc-500">{version}</span>
            </h1>
            {spec && <SourceBadge source={spec.source} />}
            <Link href={`/apis/${encodeURIComponent(service)}/${encodeURIComponent(version)}`} className="ml-auto text-sm underline">
              View API reference
            </Link>
          </div>
          {spec?.contentUpdatedAt && (
            <p className="mt-1 text-sm text-zinc-500">Last edited in the portal {new Date(spec.contentUpdatedAt).toLocaleString()}.</p>
          )}
        </div>
        {content.loading && <p className="text-sm text-zinc-500">Loading…</p>}
        <ErrorText>{content.status === 404 ? "Spec not found." : content.error}</ErrorText>
        {initial !== null && spec && (
          // Keyed on the served content so a save or revert re-initialises the editor from the server.
          <SpecContentForm
            key={initial}
            service={service}
            version={version}
            initial={initial}
            canRevert={spec.source === "overridden"}
            onChanged={reload}
            onEdit={() => setNotice(null)}
          />
        )}
        {notice && <p className="text-sm text-zinc-500">{notice}</p>}
      </main>
    </AdminOnly>
  );
}

function SpecContentForm(props: {
  service: string;
  version: string;
  initial: string;
  canRevert: boolean;
  onChanged: (message: string) => void;
  onEdit: () => void;
}) {
  const { api } = useAuth();
  const [value, setValue] = useState(props.initial);
  const [error, setError] = useState<string | null>(null);
  const dirty = value !== props.initial;

  async function run(action: () => Promise<void>, done: string) {
    setError(null);
    try {
      await action();
      props.onChanged(done);
    } catch (e) {
      setError(errorMessage(e));
    }
  }

  function revert() {
    if (!confirm("Discard the portal edits and serve the ingested file again?")) return;
    void run(() => api.admin.revertSpecContent(props.service, props.version), "Reverted to the ingested file.");
  }

  return (
    <div className="space-y-3">
      <SpecJsonEditor
        value={value}
        onChange={(v) => {
          setValue(v);
          props.onEdit();
        }}
      />
      <div className="flex items-center gap-3">
        <button
          onClick={() => run(() => api.admin.updateSpecContent(props.service, props.version, value), "Saved.")}
          disabled={!dirty || validateSpecJson(value) !== null}
          className={btnCls}
        >
          Save
        </button>
        {props.canRevert && (
          <button onClick={revert} className={secondaryBtnCls}>
            Revert to ingested
          </button>
        )}
        <ErrorText>{error}</ErrorText>
      </div>
    </div>
  );
}
