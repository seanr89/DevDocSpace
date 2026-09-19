"use client";

import { useEffect } from "react";
import { usePathname, useRouter } from "next/navigation";
import { useAuth } from "@/lib/auth";

export default function ProtectedLayout({ children }: { children: React.ReactNode }) {
  const { loading, firebaseUser } = useAuth();
  const router = useRouter();
  const pathname = usePathname();

  useEffect(() => {
    if (!loading && !firebaseUser) router.replace(`/sign-in?next=${encodeURIComponent(pathname)}`);
  }, [loading, firebaseUser, pathname, router]);

  if (loading || !firebaseUser) {
    return <div className="p-8 text-sm text-zinc-500">Loading…</div>;
  }
  return <>{children}</>;
}
