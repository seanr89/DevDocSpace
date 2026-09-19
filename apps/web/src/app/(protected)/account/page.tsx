"use client";

import { useState } from "react";
import { useAuth } from "@/lib/auth";
import { useApiData } from "@/lib/use-api-data";

export default function AccountPage() {
  const { api, me } = useAuth();
  const keys = useApiData((a) => a.apiKeys(), []);
  const calls = useApiData((a) => a.calls(50), []);
  const [name, setName] = useState("");
  const [newKey, setNewKey] = useState<string | null>(null);
  const [error, setError] = useState<string | null>(null);

  async function create(e: React.FormEvent) {
    e.preventDefault();
    setError(null);
    try {
      const created = await api.createApiKey(name);
      setNewKey(created.key);
      setName("");
      keys.reload();
    } catch (err) {
      setError(err instanceof Error ? err.message : String(err));
    }
  }

  async function revoke(id: string) {
    await api.revokeApiKey(id);
    keys.reload();
  }

  return (
    <main className="mx-auto max-w-5xl px-4 py-10 space-y-10">
      <section>
        <h1 className="text-3xl font-semibold tracking-tight">Account</h1>
        <p className="mt-2 text-sm text-zinc-500">
          {me?.email} · {me?.role}
        </p>
      </section>

      <section>
        <h2 className="text-xl font-semibold">API keys</h2>
        <form onSubmit={create} className="mt-3 flex gap-2">
          <input
            required
            placeholder="Key name (e.g. CI)"
            value={name}
            onChange={(e) => setName(e.target.value)}
            className="rounded border border-zinc-300 dark:border-zinc-700 bg-transparent px-3 py-1.5 text-sm"
          />
          <button type="submit" className="rounded bg-zinc-900 text-white dark:bg-white dark:text-black px-3 py-1.5 text-sm font-medium">
            Create key
          </button>
        </form>
        {error && <p className="mt-2 text-sm text-red-600">{error}</p>}
        {newKey && (
          <div className="mt-3 rounded border border-amber-400 bg-amber-50 dark:bg-amber-950 p-3 text-sm">
            Copy your new key now — it will not be shown again.
            <code className="mt-2 block break-all font-mono">{newKey}</code>
          </div>
        )}
        <table className="mt-4 w-full text-sm">
          <thead className="text-left text-zinc-500">
            <tr>
              <th className="py-1">Name</th>
              <th>Prefix</th>
              <th>Created</th>
              <th>Status</th>
              <th />
            </tr>
          </thead>
          <tbody>
            {keys.data?.map((k) => (
              <tr key={k.id} className="border-t border-zinc-200 dark:border-zinc-800">
                <td className="py-1.5">{k.name}</td>
                <td className="font-mono">{k.prefix}…</td>
                <td>{new Date(k.createdAt).toLocaleDateString()}</td>
                <td>{k.revokedAt ? "Revoked" : "Active"}</td>
                <td className="text-right">
                  {!k.revokedAt && (
                    <button onClick={() => revoke(k.id)} className="text-red-600 underline">
                      Revoke
                    </button>
                  )}
                </td>
              </tr>
            ))}
            {keys.data?.length === 0 && (
              <tr>
                <td colSpan={5} className="py-2 text-zinc-500">
                  No keys yet.
                </td>
              </tr>
            )}
          </tbody>
        </table>
      </section>

      <section>
        <h2 className="text-xl font-semibold">Recent API calls</h2>
        <table className="mt-3 w-full text-sm">
          <thead className="text-left text-zinc-500">
            <tr>
              <th className="py-1">When</th>
              <th>Service</th>
              <th>Env</th>
              <th>Request</th>
              <th>Status</th>
              <th>Time</th>
            </tr>
          </thead>
          <tbody>
            {calls.data?.map((c) => (
              <tr key={c.id} className="border-t border-zinc-200 dark:border-zinc-800">
                <td className="py-1.5">{new Date(c.at).toLocaleString()}</td>
                <td>
                  {c.service} {c.version}
                </td>
                <td>{c.environment}</td>
                <td className="font-mono">
                  {c.method} {c.path}
                </td>
                <td>{c.statusCode}</td>
                <td>{c.durationMs} ms</td>
              </tr>
            ))}
            {calls.data?.length === 0 && (
              <tr>
                <td colSpan={6} className="py-2 text-zinc-500">
                  No calls yet.
                </td>
              </tr>
            )}
          </tbody>
        </table>
      </section>
    </main>
  );
}
