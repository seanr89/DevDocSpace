"use client";

import Link from "next/link";
import type { DocEntry } from "@/lib/api";

export function DocTree({ ns, entries, current }: { ns: string; entries: DocEntry[]; current: string }) {
  return (
    <ul className="space-y-1 text-sm">
      {entries.map((e) =>
        e.isDirectory ? (
          <li key={e.path}>
            <div className="mt-3 mb-1 text-xs font-semibold uppercase tracking-wide text-zinc-500">{e.title}</div>
            <div className="pl-2 border-l border-zinc-200 dark:border-zinc-800">
              <DocTree ns={ns} entries={e.children} current={current} />
            </div>
          </li>
        ) : (
          <li key={e.path}>
            <Link
              href={`/docs/${ns}/${e.path}`}
              className={`block rounded px-2 py-1 ${
                current === e.path ? "bg-zinc-100 dark:bg-zinc-800 font-medium" : "text-zinc-600 dark:text-zinc-400 hover:text-inherit"
              }`}
            >
              {e.title}
            </Link>
          </li>
        ),
      )}
    </ul>
  );
}
