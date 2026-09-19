"use client";

import Link from "next/link";
import { usePathname } from "next/navigation";
import { useAuth } from "@/lib/auth";

const links = [
  { href: "/docs", label: "Docs" },
  { href: "/apis", label: "APIs" },
  { href: "/account", label: "Account" },
];

export function Nav() {
  const pathname = usePathname();
  const { me, firebaseUser, signOut } = useAuth();

  return (
    <header className="border-b border-zinc-200 dark:border-zinc-800">
      <div className="mx-auto max-w-7xl px-4 h-14 flex items-center gap-6">
        <Link href="/" className="font-semibold tracking-tight">
          DevDocSpace
        </Link>
        <nav className="flex items-center gap-4 text-sm">
          {links.map((l) => (
            <Link
              key={l.href}
              href={l.href}
              className={pathname.startsWith(l.href) ? "font-medium" : "text-zinc-500 hover:text-inherit"}
            >
              {l.label}
            </Link>
          ))}
          {me?.role === "Admin" && (
            <Link href="/admin" className={pathname.startsWith("/admin") ? "font-medium" : "text-zinc-500 hover:text-inherit"}>
              Admin
            </Link>
          )}
        </nav>
        <div className="ml-auto text-sm flex items-center gap-3">
          {firebaseUser ? (
            <>
              <span className="text-zinc-500">
                {me?.email ?? firebaseUser.email}
                {me && <span className="ml-2 rounded bg-zinc-100 dark:bg-zinc-800 px-1.5 py-0.5 text-xs">{me.role}</span>}
              </span>
              <button onClick={signOut} className="underline">
                Sign out
              </button>
            </>
          ) : (
            <Link href="/sign-in" className="underline">
              Sign in
            </Link>
          )}
        </div>
      </div>
    </header>
  );
}
