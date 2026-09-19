"use client";

import Link from "next/link";
import { useApiData } from "@/lib/use-api-data";

export default function ApisIndexPage() {
  const { data, error, loading } = useApiData((api) => api.specs(), []);
  const services = new Map<string, typeof data>();
  for (const s of data ?? []) services.set(s.service, [...(services.get(s.service) ?? []), s]);

  return (
    <main className="mx-auto max-w-5xl px-4 py-10">
      <h1 className="text-3xl font-semibold tracking-tight">APIs</h1>
      {loading && <p className="mt-6 text-sm text-zinc-500">Loading…</p>}
      {error && <p className="mt-6 text-sm text-red-600">{error}</p>}
      <ul className="mt-6 grid gap-3 sm:grid-cols-2">
        {[...services.entries()].map(([service, versions]) => (
          <li key={service} className="rounded border border-zinc-200 dark:border-zinc-800 p-4">
            <div className="font-medium">{service}</div>
            <div className="mt-2 flex flex-wrap gap-2">
              {versions!.map((v) => (
                <Link
                  key={v.version}
                  href={`/apis/${service}/${v.version}`}
                  className="rounded bg-zinc-100 dark:bg-zinc-800 px-2 py-0.5 text-xs font-medium"
                >
                  {v.version}
                </Link>
              ))}
            </div>
          </li>
        ))}
        {data?.length === 0 && <li className="text-sm text-zinc-500">No API specifications are visible to you.</li>}
      </ul>
    </main>
  );
}
