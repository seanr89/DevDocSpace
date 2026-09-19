"use client";

import { useEffect, useState } from "react";
import { useAuth } from "./auth";
import { ApiError, type ApiClient } from "./api";

type State<T> = { data: T | null; error: Error | null };

export function useApiData<T>(load: (api: ApiClient) => Promise<T>, deps: unknown[]) {
  const { api, firebaseUser } = useAuth();
  const [state, setState] = useState<State<T>>({ data: null, error: null });
  const [version, setVersion] = useState(0);

  useEffect(() => {
    if (!firebaseUser) return;
    let cancelled = false;
    load(api).then(
      (data) => !cancelled && setState({ data, error: null }),
      (e) => !cancelled && setState({ data: null, error: e instanceof Error ? e : new Error(String(e)) }),
    );
    return () => {
      cancelled = true;
    };
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [api, firebaseUser, version, ...deps]);

  return {
    data: state.data,
    error: state.error?.message ?? null,
    status: state.error instanceof ApiError ? state.error.status : null,
    loading: state.data === null && state.error === null,
    reload: () => setVersion((v) => v + 1),
  };
}
