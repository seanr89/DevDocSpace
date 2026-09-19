import Link from "next/link";

export default function Home() {
  return (
    <main className="mx-auto max-w-3xl px-4 py-20">
      <h1 className="text-4xl font-semibold tracking-tight">DevDocSpace</h1>
      <p className="mt-4 text-lg text-zinc-600 dark:text-zinc-400">
        Documentation, API references and a live console for every service — in one place.
      </p>
      <div className="mt-8 flex gap-4">
        <Link href="/docs" className="rounded bg-zinc-900 text-white dark:bg-white dark:text-black px-4 py-2 text-sm font-medium">
          Browse docs
        </Link>
        <Link href="/apis" className="rounded border border-zinc-300 dark:border-zinc-700 px-4 py-2 text-sm font-medium">
          Explore APIs
        </Link>
      </div>
    </main>
  );
}
