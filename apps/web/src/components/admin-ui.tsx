"use client";

import type { ReactNode } from "react";
import { useAuth } from "@/lib/auth";
import type { ContentSource, Role } from "@/lib/api";

export const ROLES: Role[] = ["ExternalClient", "InternalDeveloper", "Admin"];

export const inputCls = "rounded border border-zinc-300 dark:border-zinc-700 bg-transparent px-2 py-1 text-sm";
export const btnCls =
  "rounded bg-zinc-900 text-white dark:bg-white dark:text-black px-3 py-1 text-sm font-medium disabled:opacity-40";
export const secondaryBtnCls = "rounded border border-zinc-300 dark:border-zinc-700 px-3 py-1 text-sm disabled:opacity-40";
export const textareaCls = `${inputCls} w-full font-mono text-xs leading-5`;

// Renders children only for admins; the API enforces the same rule, this just avoids showing broken pages.
export function AdminOnly({ children }: { children: ReactNode }) {
  const { me } = useAuth();
  if (me && me.role !== "Admin") return <main className="p-8 text-sm text-red-600">Admin access required.</main>;
  return <>{children}</>;
}

const SOURCE_STYLES: Record<ContentSource, { label: string; cls: string; title: string }> = {
  ingested: {
    label: "ingested",
    cls: "bg-zinc-100 text-zinc-700 dark:bg-zinc-800 dark:text-zinc-300",
    title: "Served from the content store (CI ingestion).",
  },
  managed: {
    label: "portal",
    cls: "bg-sky-100 text-sky-800 dark:bg-sky-900 dark:text-sky-200",
    title: "Created in the portal; stored only in the database.",
  },
  overridden: {
    label: "overridden",
    cls: "bg-amber-100 text-amber-800 dark:bg-amber-900 dark:text-amber-200",
    title: "Edited in the portal; the database version is served instead of the ingested file.",
  },
};

export function SourceBadge({ source }: { source: ContentSource }) {
  const s = SOURCE_STYLES[source];
  return (
    <span title={s.title} className={`rounded px-1.5 py-0.5 text-xs ${s.cls}`}>
      {s.label}
    </span>
  );
}

export function ErrorText({ children }: { children: ReactNode }) {
  return children ? <p className="text-sm text-red-600">{children}</p> : null;
}

export function errorMessage(e: unknown): string {
  return e instanceof Error ? e.message : String(e);
}
