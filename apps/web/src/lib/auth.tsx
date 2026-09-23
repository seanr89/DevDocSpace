"use client";

import { createContext, useCallback, useContext, useEffect, useMemo, useState } from "react";
import {
  GoogleAuthProvider,
  onAuthStateChanged,
  signInWithEmailAndPassword,
  signInWithPopup,
  signOut as firebaseSignOut,
} from "firebase/auth";
import { getFirebaseAuth } from "./firebase";
import { bearerHeaders, createApiClient, type ApiClient, type AuthHeaderProvider, type Me } from "./api";
import { clearDevUser, devAuthHeaders, isDevAuthEnabled, loadDevUser, saveDevUser } from "./dev-auth";

export type AuthUser = { uid: string; email: string | null };

type AuthState = {
  loading: boolean;
  configError: string | null;
  // True when the app is using the local dev-auth stand-in instead of Firebase.
  devAuth: boolean;
  user: AuthUser | null;
  me: Me | null;
  api: ApiClient;
  getAuthHeaders: AuthHeaderProvider;
  signInWithGoogle: () => Promise<void>;
  signInWithPassword: (email: string, password: string) => Promise<void>;
  signInAsDevUser: (email: string) => Promise<void>;
  signOut: () => Promise<void>;
};

const AuthContext = createContext<AuthState | null>(null);

export function AuthProvider({ children }: { children: React.ReactNode }) {
  const [devAuth] = useState(isDevAuthEnabled);
  const [user, setUser] = useState<AuthUser | null>(null);
  const [me, setMe] = useState<Me | null>(null);
  const [configError] = useState<string | null>(() => {
    if (devAuth) return null;
    try {
      getFirebaseAuth();
      return null;
    } catch (e) {
      return e instanceof Error ? e.message : String(e);
    }
  });
  const [loading, setLoading] = useState(configError === null);

  const getAuthHeaders = useMemo<AuthHeaderProvider>(
    () =>
      devAuth
        ? async () => {
            const email = loadDevUser();
            return email ? devAuthHeaders(email) : {};
          }
        : bearerHeaders(async () => getFirebaseAuth().currentUser?.getIdToken()),
    [devAuth],
  );
  const api = useMemo(() => createApiClient(getAuthHeaders), [getAuthHeaders]);

  const loadMe = useCallback(async () => {
    try {
      setMe(await api.me());
    } catch {
      setMe(null);
    }
  }, [api]);

  // Firebase mode: follow the SDK's auth state.
  useEffect(() => {
    if (devAuth || configError) return;
    return onAuthStateChanged(getFirebaseAuth(), async (fbUser) => {
      setUser(fbUser ? { uid: fbUser.uid, email: fbUser.email } : null);
      if (fbUser) await loadMe();
      else setMe(null);
      setLoading(false);
    });
  }, [devAuth, configError, loadMe]);

  // Dev mode: restore whichever preset user was picked last.
  useEffect(() => {
    if (!devAuth) return;
    const email = loadDevUser();
    let cancelled = false;
    (async () => {
      if (email) {
        setUser({ uid: `dev:${email}`, email });
        await loadMe();
      }
      if (!cancelled) setLoading(false);
    })();
    return () => {
      cancelled = true;
    };
  }, [devAuth, loadMe]);

  const signInWithGoogle = useCallback(async () => {
    await signInWithPopup(getFirebaseAuth(), new GoogleAuthProvider());
  }, []);
  const signInWithPassword = useCallback(async (email: string, password: string) => {
    await signInWithEmailAndPassword(getFirebaseAuth(), email, password);
  }, []);
  const signInAsDevUser = useCallback(
    async (email: string) => {
      if (!devAuth) throw new Error("Dev auth is not enabled");
      saveDevUser(email);
      setUser({ uid: `dev:${email}`, email });
      await loadMe();
    },
    [devAuth, loadMe],
  );
  const signOut = useCallback(async () => {
    if (devAuth) {
      clearDevUser();
      setUser(null);
      setMe(null);
      return;
    }
    await firebaseSignOut(getFirebaseAuth());
  }, [devAuth]);

  const value = useMemo(
    () => ({
      loading, configError, devAuth, user, me, api, getAuthHeaders,
      signInWithGoogle, signInWithPassword, signInAsDevUser, signOut,
    }),
    [loading, configError, devAuth, user, me, api, getAuthHeaders, signInWithGoogle, signInWithPassword, signInAsDevUser, signOut],
  );

  return <AuthContext.Provider value={value}>{children}</AuthContext.Provider>;
}

export function useAuth(): AuthState {
  const ctx = useContext(AuthContext);
  if (!ctx) throw new Error("useAuth must be used within AuthProvider");
  return ctx;
}
