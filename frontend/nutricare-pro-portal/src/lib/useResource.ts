"use client";
import { useCallback, useEffect, useState } from "react";
import { api, errorMessage } from "./api";
export function useResource<T>(path: string | null) {
  const [data, setData] = useState<T | null>(null), [loading, setLoading] = useState(Boolean(path)), [error, setError] = useState("");
  const reload = useCallback(async () => {
    if (!path) return;
    setLoading(true); setError("");
    try { const result = await api<T>(path); setData(result); return result; } catch(e) { setError(errorMessage(e)); } finally { setLoading(false); }
  }, [path]);
  useEffect(() => { setData(null); void reload(); }, [reload]);
  return { data, loading, error, reload, setData };
}
