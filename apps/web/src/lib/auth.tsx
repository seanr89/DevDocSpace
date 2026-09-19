"use client";

import { createContext, useCallback, useContext, useEffect, useMemo, useState } from "react";
import {
  GoogleAuthProvider,
  onAuthStateChanged,
  signInWithEmailAndPassword,
  signInWithPopup,
  signOut as firebaseSignOut,
  type User as FirebaseUser,
} from "firebase/auth";
import { getFirebaseAuth } from "./firebase";
import { createApiClient, type ApiClient, type Me } from "./api";

type AuthState = {
  loading: boolean;
  configError: string | null;
  firebaseUser: FirebaseUser | null;
  me: Me | null;
  api: ApiClient;
  signInWithGoogle: () => Promise<void>;
  signInWithPassword: (email: string, password: string) => Promise<void>;
  signOut: () => Promise<void>;
};

const AuthContext = createContext<AuthState | null>(null);

export function AuthProvider({ children }: { children: React.ReactNode }) {
  const [firebaseUser, setFirebaseUser] = useState<FirebaseUser | null>(null);
  const [me, setMe] = useState<Me | null>(null);
  const [configError] = useState<string | null>(() => {
    try {
      getFirebaseAuth();
      return null;
    } catch (e) {
      return e instanceof Error ? e.message : String(e);
    }
  });
  const [loading, setLoading] = useState(configError === null);

  const api = useMemo(
    () => createApiClient(async () => getFirebaseAuth().currentUser?.getIdToken() ?? null),
    [],
  );

  useEffect(() => {
    if (configError) return;
    return onAuthStateChanged(getFirebaseAuth(), async (user) => {
      setFirebaseUser(user);
      if (user) {
        try {
          setMe(await api.me());
        } catch {
          setMe(null);
        }
      } else {
        setMe(null);
      }
      setLoading(false);
    });
  }, [api, configError]);

  const signInWithGoogle = useCallback(async () => {
    await signInWithPopup(getFirebaseAuth(), new GoogleAuthProvider());
  }, []);
  const signInWithPassword = useCallback(async (email: string, password: string) => {
    await signInWithEmailAndPassword(getFirebaseAuth(), email, password);
  }, []);
  const signOut = useCallback(async () => {
    await firebaseSignOut(getFirebaseAuth());
  }, []);

  const value = useMemo(
    () => ({ loading, configError, firebaseUser, me, api, signInWithGoogle, signInWithPassword, signOut }),
    [loading, configError, firebaseUser, me, api, signInWithGoogle, signInWithPassword, signOut],
  );

  return <AuthContext.Provider value={value}>{children}</AuthContext.Provider>;
}

export function useAuth(): AuthState {
  const ctx = useContext(AuthContext);
  if (!ctx) throw new Error("useAuth must be used within AuthProvider");
  return ctx;
}
