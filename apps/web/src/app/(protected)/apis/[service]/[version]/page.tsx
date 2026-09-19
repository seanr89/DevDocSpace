"use client";

import { Suspense, use } from "react";
import { useRouter, useSearchParams } from "next/navigation";
import { ENVIRONMENTS, type ApiEnvironment } from "@/lib/api";
import { useApiData } from "@/lib/use-api-data";
import { SpecViewer } from "@/components/spec-viewer";

function SpecPageInner({ params }: { params: Promise<{ service: string; version: string }> }) {
  const { service, version } = use(params);
  const router = useRouter();
  const search = useSearchParams();
  const envParam = search.get("env");
  const env: ApiEnvironment = ENVIRONMENTS.find((e) => e.toLowerCase() === envParam?.toLowerCase()) ?? "Sandbox";

  const specs = useApiData((api) => api.specs(), []);
  const spec = useApiData((api) => api.spec(service, version), [service, version]);

  const versions = specs.data?.filter((s) => s.service === service) ?? [];
  const available = versions.find((v) => v.version === version)?.environments ?? [];

  function setQuery(next: { version?: string; env?: ApiEnvironment }) {
    const v = next.version ?? version;
    const e = next.env ?? env;
    router.push(`/apis/${service}/${v}?env=${e.toLowerCase()}`);
  }

  return (
    <div className="mx-auto max-w-7xl px-4 py-6">
      <div className="flex flex-wrap items-center gap-4 border-b border-zinc-200 dark:border-zinc-800 pb-4">
        <h1 className="text-2xl font-semibold tracking-tight">{service}</h1>
        <label className="text-sm flex items-center gap-2">
          Version
          <select
            value={version}
            onChange={(e) => setQuery({ version: e.target.value })}
            className="rounded border border-zinc-300 dark:border-zinc-700 bg-transparent px-2 py-1"
          >
            {versions.length === 0 && <option value={version}>{version}</option>}
            {versions.map((v) => (
              <option key={v.version} value={v.version}>
                {v.version}
              </option>
            ))}
          </select>
        </label>
        <div className="text-sm flex items-center gap-1" role="radiogroup" aria-label="Environment">
          {ENVIRONMENTS.map((e) => {
            const enabled = available.includes(e);
            return (
              <button
                key={e}
                role="radio"
                aria-checked={env === e}
                disabled={!enabled}
                onClick={() => setQuery({ env: e })}
                title={enabled ? undefined : "Not configured for this API"}
                className={`rounded px-3 py-1 ${
                  env === e ? "bg-zinc-900 text-white dark:bg-white dark:text-black" : "border border-zinc-300 dark:border-zinc-700"
                } disabled:opacity-40`}
              >
                {e}
              </button>
            );
          })}
        </div>
      </div>

      {spec.loading && <p className="mt-6 text-sm text-zinc-500">Loading spec…</p>}
      {spec.error && <p className="mt-6 text-sm text-red-600">{spec.status === 404 ? "Spec not found." : spec.error}</p>}
      {spec.data && <SpecViewer spec={spec.data} service={service} version={version} env={env} />}
    </div>
  );
}

export default function SpecPage(props: { params: Promise<{ service: string; version: string }> }) {
  return (
    <Suspense>
      <SpecPageInner {...props} />
    </Suspense>
  );
}
