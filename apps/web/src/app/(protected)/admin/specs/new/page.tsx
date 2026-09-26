"use client";

import { useState } from "react";
import Link from "next/link";
import { useRouter } from "next/navigation";
import { useAuth } from "@/lib/auth";
import type { Role } from "@/lib/api";
import { isValidSlug, validateSpecJson } from "@/lib/content-validation";
import { AdminOnly, ErrorText, ROLES, btnCls, errorMessage, inputCls } from "@/components/admin-ui";
import { SpecJsonEditor } from "@/components/spec-json-editor";

export default function NewSpecPage() {
  const { api } = useAuth();
  const router = useRouter();
  const [service, setService] = useState("");
  const [version, setVersion] = useState("");
  const [role, setRole] = useState<Role>("InternalDeveloper");
  const [content, setContent] = useState("");
  const [error, setError] = useState<string | null>(null);
  const [saving, setSaving] = useState(false);

  const keyError =
    (service && !isValidSlug(service)) || (version && !isValidSlug(version))
      ? "Service and version must be lowercase slugs (a-z, 0-9, '.', '_', '-')."
      : null;
  const canSave = !saving && !keyError && service && version && validateSpecJson(content) === null;

  async function create(e: React.FormEvent) {
    e.preventDefault();
    setSaving(true);
    setError(null);
    try {
      await api.admin.createSpec({ service, version, requiredRole: role, content });
      router.push(`/admin/specs/${encodeURIComponent(service)}/${encodeURIComponent(version)}`);
    } catch (err) {
      setError(errorMessage(err));
      setSaving(false);
    }
  }

  return (
    <AdminOnly>
      <main className="mx-auto max-w-6xl px-4 py-10 space-y-6">
        <div>
          <Link href="/admin" className="text-sm text-zinc-500 hover:underline">
            ← Admin
          </Link>
          <h1 className="mt-2 text-3xl font-semibold tracking-tight">New API spec</h1>
          <p className="mt-1 text-sm text-zinc-500">
            Portal-created specs are stored in the database. If CI later ingests a file for the same service and version, the
            portal version keeps being served until you revert it.
          </p>
        </div>
        <form onSubmit={create} className="space-y-4">
          <div className="flex flex-wrap items-center gap-3">
            <input aria-label="Service" required placeholder="service" value={service} onChange={(e) => setService(e.target.value.trim())} className={inputCls} />
            <input aria-label="Version" required placeholder="version (e.g. v1)" value={version} onChange={(e) => setVersion(e.target.value.trim())} className={inputCls} />
            <label className="text-sm flex items-center gap-2">
              Required role
              <select value={role} onChange={(e) => setRole(e.target.value as Role)} className={inputCls}>
                {ROLES.map((r) => (
                  <option key={r}>{r}</option>
                ))}
              </select>
            </label>
          </div>
          <ErrorText>{keyError}</ErrorText>
          <SpecJsonEditor value={content} onChange={setContent} />
          <div className="flex items-center gap-3">
            <button type="submit" disabled={!canSave} className={btnCls}>
              {saving ? "Creating…" : "Create spec"}
            </button>
            <ErrorText>{error}</ErrorText>
          </div>
        </form>
      </main>
    </AdminOnly>
  );
}
