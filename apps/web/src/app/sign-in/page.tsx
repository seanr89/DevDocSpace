"use client";

import { Suspense, useEffect, useState } from "react";
import { useRouter, useSearchParams } from "next/navigation";
import { useAuth } from "@/lib/auth";
import { DEV_USERS } from "@/lib/dev-auth";

function SignInForm() {
  const { user, loading, configError, devAuth, signInWithGoogle, signInWithPassword, signInAsDevUser } = useAuth();
  const router = useRouter();
  const params = useSearchParams();
  const next = params.get("next") ?? "/docs";
  const [email, setEmail] = useState("");
  const [password, setPassword] = useState("");
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    if (!loading && user) router.replace(next);
  }, [loading, user, next, router]);

  async function run(action: () => Promise<void>) {
    setError(null);
    try {
      await action();
    } catch (e) {
      setError(e instanceof Error ? e.message : "Sign-in failed");
    }
  }

  if (devAuth) {
    return (
      <main className="mx-auto max-w-sm px-4 py-20">
        <h1 className="text-2xl font-semibold">Sign in</h1>
        <div className="mt-4 rounded border border-amber-300 bg-amber-50 dark:bg-amber-950 dark:border-amber-800 p-3 text-sm text-amber-800 dark:text-amber-200">
          <p className="font-medium">Local development sign-in</p>
          <p className="mt-1">
            Firebase is not configured, so the app is using the dev-auth stand-in. Pick a preset user; the role is seeded by
            the API&apos;s appsettings.Development.json.
          </p>
        </div>
        <div className="mt-6 space-y-2">
          {DEV_USERS.map((u) => (
            <button
              key={u.email}
              onClick={() => run(() => signInAsDevUser(u.email))}
              className="w-full rounded border border-zinc-300 dark:border-zinc-700 px-4 py-2 text-left text-sm"
            >
              <span className="font-medium">{u.label}</span>
              <span className="ml-2 text-zinc-500">{u.email}</span>
            </button>
          ))}
        </div>
        <div className="my-6 text-center text-xs text-zinc-500">or any email</div>
        <form
          className="flex gap-2"
          onSubmit={(e) => {
            e.preventDefault();
            run(() => signInAsDevUser(email.trim()));
          }}
        >
          <input
            type="email"
            required
            placeholder="you@example.com"
            value={email}
            onChange={(e) => setEmail(e.target.value)}
            className="flex-1 rounded border border-zinc-300 dark:border-zinc-700 bg-transparent px-3 py-2 text-sm"
          />
          <button type="submit" className="rounded bg-zinc-900 text-white dark:bg-white dark:text-black px-4 py-2 text-sm font-medium">
            Sign in
          </button>
        </form>
        {error && <p className="mt-4 text-sm text-red-600">{error}</p>}
      </main>
    );
  }

  return (
    <main className="mx-auto max-w-sm px-4 py-20">
      <h1 className="text-2xl font-semibold">Sign in</h1>
      {configError && (
        <p className="mt-4 rounded border border-red-300 bg-red-50 dark:bg-red-950 p-3 text-sm text-red-700 dark:text-red-300">
          Firebase is not configured: {configError}. Set the NEXT_PUBLIC_FIREBASE_* variables in .env.local.
        </p>
      )}
      <button
        onClick={() => run(signInWithGoogle)}
        className="mt-6 w-full rounded border border-zinc-300 dark:border-zinc-700 px-4 py-2 text-sm font-medium"
      >
        Continue with Google
      </button>
      <div className="my-6 text-center text-xs text-zinc-500">or</div>
      <form
        className="space-y-3"
        onSubmit={(e) => {
          e.preventDefault();
          run(() => signInWithPassword(email, password));
        }}
      >
        <input
          type="email"
          required
          placeholder="Email"
          value={email}
          onChange={(e) => setEmail(e.target.value)}
          className="w-full rounded border border-zinc-300 dark:border-zinc-700 bg-transparent px-3 py-2 text-sm"
        />
        <input
          type="password"
          required
          placeholder="Password"
          value={password}
          onChange={(e) => setPassword(e.target.value)}
          className="w-full rounded border border-zinc-300 dark:border-zinc-700 bg-transparent px-3 py-2 text-sm"
        />
        <button type="submit" className="w-full rounded bg-zinc-900 text-white dark:bg-white dark:text-black px-4 py-2 text-sm font-medium">
          Sign in with email
        </button>
      </form>
      {error && <p className="mt-4 text-sm text-red-600">{error}</p>}
    </main>
  );
}

export default function SignInPage() {
  return (
    <Suspense>
      <SignInForm />
    </Suspense>
  );
}
