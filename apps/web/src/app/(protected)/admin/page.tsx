"use client";

import { useState } from "react";
import { useAuth } from "@/lib/auth";
import { useApiData } from "@/lib/use-api-data";
import { ENVIRONMENTS, type AdminSpec, type ApiEnvironment, type Role } from "@/lib/api";

const ROLES: Role[] = ["ExternalClient", "InternalDeveloper", "Admin"];

const inputCls = "rounded border border-zinc-300 dark:border-zinc-700 bg-transparent px-2 py-1 text-sm";
const btnCls = "rounded bg-zinc-900 text-white dark:bg-white dark:text-black px-3 py-1 text-sm font-medium";

export default function AdminPage() {
  const { me } = useAuth();
  if (me && me.role !== "Admin") return <main className="p-8 text-sm text-red-600">Admin access required.</main>;

  return (
    <main className="mx-auto max-w-6xl px-4 py-10 space-y-12">
      <h1 className="text-3xl font-semibold tracking-tight">Admin</h1>
      <SpecsSection />
      <NamespacesSection />
      <UsersSection />
    </main>
  );
}

function SpecsSection() {
  const { api } = useAuth();
  const specs = useApiData((a) => a.admin.specs(), []);
  const [syncResult, setSyncResult] = useState<string | null>(null);

  async function sync() {
    const r = await api.admin.syncSpecs();
    setSyncResult(`Discovered ${r.discovered}, added ${r.added}.`);
    specs.reload();
  }

  return (
    <section>
      <div className="flex items-center gap-4">
        <h2 className="text-xl font-semibold">API spec registry</h2>
        <button onClick={sync} className={btnCls}>
          Sync from content store
        </button>
        {syncResult && <span className="text-sm text-zinc-500">{syncResult}</span>}
      </div>
      {specs.error && <p className="mt-2 text-sm text-red-600">{specs.error}</p>}
      <div className="mt-4 space-y-4">
        {specs.data?.map((s) => (
          <SpecEditor key={s.id} spec={s} onSaved={specs.reload} />
        ))}
      </div>
    </section>
  );
}

function SpecEditor({ spec, onSaved }: { spec: AdminSpec; onSaved: () => void }) {
  const { api } = useAuth();
  const [role, setRole] = useState<Role>(spec.requiredRole);
  const [envs, setEnvs] = useState<Record<ApiEnvironment, { baseUrl: string; credentialKey: string }>>(() => {
    const init = Object.fromEntries(ENVIRONMENTS.map((e) => [e, { baseUrl: "", credentialKey: "" }])) as Record<
      ApiEnvironment,
      { baseUrl: string; credentialKey: string }
    >;
    for (const e of spec.environments) init[e.name] = { baseUrl: e.baseUrl, credentialKey: e.credentialKey ?? "" };
    return init;
  });
  const [saved, setSaved] = useState(false);

  async function save() {
    await api.admin.updateSpec(spec.service, spec.version, {
      requiredRole: role,
      environments: ENVIRONMENTS.filter((e) => envs[e].baseUrl.trim()).map((e) => ({
        name: e,
        baseUrl: envs[e].baseUrl.trim(),
        credentialKey: envs[e].credentialKey.trim() || null,
      })),
    });
    setSaved(true);
    onSaved();
  }

  return (
    <div className="rounded border border-zinc-200 dark:border-zinc-800 p-4">
      <div className="flex items-center gap-4">
        <span className="font-medium">
          {spec.service} <span className="text-zinc-500">{spec.version}</span>
        </span>
        <label className="text-sm flex items-center gap-2">
          Required role
          <select value={role} onChange={(e) => setRole(e.target.value as Role)} className={inputCls}>
            {ROLES.map((r) => (
              <option key={r}>{r}</option>
            ))}
          </select>
        </label>
        <span className="ml-auto text-xs text-zinc-500 font-mono">{spec.path}</span>
      </div>
      <div className="mt-3 grid gap-2 sm:grid-cols-3">
        {ENVIRONMENTS.map((e) => (
          <div key={e} className="space-y-1">
            <div className="text-xs font-semibold uppercase tracking-wide text-zinc-500">{e}</div>
            <input
              placeholder="Base URL"
              value={envs[e].baseUrl}
              onChange={(ev) => setEnvs({ ...envs, [e]: { ...envs[e], baseUrl: ev.target.value } })}
              className={`${inputCls} w-full`}
            />
            <input
              placeholder="Credential key (optional)"
              value={envs[e].credentialKey}
              onChange={(ev) => setEnvs({ ...envs, [e]: { ...envs[e], credentialKey: ev.target.value } })}
              className={`${inputCls} w-full`}
            />
          </div>
        ))}
      </div>
      <div className="mt-3 flex items-center gap-3">
        <button onClick={save} className={btnCls}>
          Save
        </button>
        {saved && <span className="text-sm text-zinc-500">Saved.</span>}
      </div>
    </div>
  );
}

function NamespacesSection() {
  const { api } = useAuth();
  const namespaces = useApiData((a) => a.admin.namespaces(), []);
  const [slug, setSlug] = useState("");
  const [title, setTitle] = useState("");
  const [role, setRole] = useState<Role>("ExternalClient");

  async function upsert(e: React.FormEvent) {
    e.preventDefault();
    await api.admin.updateNamespace(slug.trim(), { title: title.trim() || null, requiredRole: role });
    setSlug("");
    setTitle("");
    namespaces.reload();
  }

  return (
    <section>
      <h2 className="text-xl font-semibold">Documentation namespaces</h2>
      <p className="mt-1 text-sm text-zinc-500">Namespaces without a policy are visible to every signed-in user.</p>
      <form onSubmit={upsert} className="mt-3 flex flex-wrap gap-2">
        <input required placeholder="slug" value={slug} onChange={(e) => setSlug(e.target.value)} className={inputCls} />
        <input placeholder="Title" value={title} onChange={(e) => setTitle(e.target.value)} className={inputCls} />
        <select value={role} onChange={(e) => setRole(e.target.value as Role)} className={inputCls}>
          {ROLES.map((r) => (
            <option key={r}>{r}</option>
          ))}
        </select>
        <button type="submit" className={btnCls}>
          Save policy
        </button>
      </form>
      <table className="mt-4 w-full text-sm">
        <thead className="text-left text-zinc-500">
          <tr>
            <th className="py-1">Slug</th>
            <th>Title</th>
            <th>Required role</th>
          </tr>
        </thead>
        <tbody>
          {namespaces.data?.map((n) => (
            <tr key={n.id} className="border-t border-zinc-200 dark:border-zinc-800">
              <td className="py-1.5 font-mono">{n.slug}</td>
              <td>{n.title ?? "—"}</td>
              <td>{n.requiredRole}</td>
            </tr>
          ))}
        </tbody>
      </table>
    </section>
  );
}

function UsersSection() {
  const { api } = useAuth();
  const users = useApiData((a) => a.admin.users(), []);

  async function setRole(id: string, role: Role) {
    await api.admin.updateUserRole(id, role);
    users.reload();
  }

  return (
    <section>
      <h2 className="text-xl font-semibold">Users</h2>
      <table className="mt-3 w-full text-sm">
        <thead className="text-left text-zinc-500">
          <tr>
            <th className="py-1">Email</th>
            <th>Role</th>
            <th>Joined</th>
          </tr>
        </thead>
        <tbody>
          {users.data?.map((u) => (
            <tr key={u.id} className="border-t border-zinc-200 dark:border-zinc-800">
              <td className="py-1.5">{u.email}</td>
              <td>
                <select value={u.role} onChange={(e) => setRole(u.id, e.target.value as Role)} className={inputCls}>
                  {ROLES.map((r) => (
                    <option key={r}>{r}</option>
                  ))}
                </select>
              </td>
              <td>{new Date(u.createdAt).toLocaleDateString()}</td>
            </tr>
          ))}
        </tbody>
      </table>
    </section>
  );
}
