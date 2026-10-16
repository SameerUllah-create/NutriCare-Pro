"use client";
import { createContext, useCallback, useContext, useEffect, useState } from "react";
import type { ReactNode } from "react";
import type { User } from "@/lib/types";

type Session = { user: User | null; loading: boolean; error: string; reload: () => Promise<void>; signOut: () => Promise<void> };
const SessionContext = createContext<Session | null>(null);
export function SessionProvider({ children }: { children: ReactNode }) {
  const [user, setUser] = useState<User | null>(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState("");
  const reload = useCallback(async () => {
    setLoading(true); setError("");
    try {
      const response = await fetch("/api/session", { cache: "no-store" });
      const result = await response.json();
      if (response.ok && result.success) setUser(result.data);
      else { setUser(null); if (response.status !== 401) setError(result.message || "Your session could not be checked."); }
    } catch { setError("Your session could not be checked. Please retry."); } finally { setLoading(false); }
  }, []);
  useEffect(() => {
    void reload();
    const expired = () => { setUser(null); setLoading(false); };
    window.addEventListener("session-expired", expired);
    // Remove tokens stored by the earlier development-only dashboard.
    window.localStorage.removeItem("accessToken");
    return () => window.removeEventListener("session-expired", expired);
  }, [reload]);
  async function signOut() {
    const response = await fetch("/api/auth/logout", { method: "POST", headers: { "Content-Type": "application/json" }, body: "{}" });
    if (!response.ok) throw new Error("Sign out could not be confirmed. Please retry.");
    setUser(null); window.location.assign("/login");
  }
  return <SessionContext.Provider value={{ user, loading, error, reload, signOut }}>{children}</SessionContext.Provider>;
}
export function useSession() { const context = useContext(SessionContext); if (!context) throw new Error("Session provider missing"); return context; }
