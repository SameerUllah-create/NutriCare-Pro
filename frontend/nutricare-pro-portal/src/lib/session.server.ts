import { NextRequest, NextResponse } from "next/server";
import { createHash } from "node:crypto";
import type { ApiEnvelope, Tokens } from "./types";

export const ACCESS_COOKIE = "nc_access";
export const REFRESH_COOKIE = "nc_refresh";
export const API_BASE_URL = (process.env.API_BASE_URL || process.env.NEXT_PUBLIC_API_BASE_URL || "http://localhost:7001").replace(/\/$/, "");
const refreshes = new Map<string, Promise<Tokens | null>>();

export function sameOrigin(request: NextRequest) {
  const origin = request.headers.get("origin");
  if (request.headers.get("sec-fetch-site") === "cross-site") return false;
  if (!origin) return true;
  try {
    // Next may normalize its internal URL to localhost. Compare the browser's
    // origin with the actual Host; forwarded hosts never establish trust.
    const host = request.headers.get("host") || request.nextUrl.host;
    if (/[\/\\@?#,]/.test(host)) return false;
    const expected = new URL(process.env.APP_ORIGIN || `${request.nextUrl.protocol}//${host}`).origin;
    return new URL(origin).origin === origin && origin === expected;
  } catch { return false; }
}

export function setSession(response: NextResponse, tokens: Tokens) {
  const options = { httpOnly: true, secure: process.env.NODE_ENV === "production", sameSite: "strict" as const, path: "/", maxAge: 60 * 60 * 24 * 7 };
  response.cookies.set(ACCESS_COOKIE, tokens.accessToken, options);
  response.cookies.set(REFRESH_COOKIE, tokens.refreshToken, options);
}
export function clearSession(response: NextResponse) {
  response.cookies.set(ACCESS_COOKIE, "", { path: "/", maxAge: 0, httpOnly: true, sameSite: "strict", secure: process.env.NODE_ENV === "production" });
  response.cookies.set(REFRESH_COOKIE, "", { path: "/", maxAge: 0, httpOnly: true, sameSite: "strict", secure: process.env.NODE_ENV === "production" });
}
export function failure(message: string, status: number) { return NextResponse.json({ success: false, message }, { status, headers: { "Cache-Control": "no-store" } }); }

export async function upstream(path: string, init: RequestInit = {}) {
  return fetch(`${API_BASE_URL}${path}`, { ...init, cache: "no-store", signal: AbortSignal.timeout(30000) });
}
async function refresh(token: string): Promise<Tokens | null> {
  const key = createHash("sha256").update(token).digest("hex");
  const current = refreshes.get(key); if (current) return current;
  const pending = (async () => {
    const response = await upstream("/api/Auth/refresh-token", { method: "POST", headers: { "Content-Type": "application/json" }, body: JSON.stringify({ refreshToken: token }) });
    const payload = await response.json() as ApiEnvelope<Tokens>;
    return response.ok && payload.success && payload.data?.accessToken ? payload.data : null;
  })();
  refreshes.set(key, pending);
  try { return await pending; } finally { setTimeout(() => refreshes.delete(key), 2000); }
}
export async function authorizedFetch(request: NextRequest, path: string, init: RequestInit = {}) {
  let access = request.cookies.get(ACCESS_COOKIE)?.value;
  const headers = new Headers(init.headers);
  if (access) headers.set("Authorization", `Bearer ${access}`);
  let response = await upstream(path, { ...init, headers });
  let tokens: Tokens | null = null;
  const refreshToken = request.cookies.get(REFRESH_COOKIE)?.value;
  if (response.status === 401 && refreshToken) {
    tokens = await refresh(refreshToken);
    if (tokens) { access = tokens.accessToken; headers.set("Authorization", `Bearer ${access}`); response = await upstream(path, { ...init, headers }); }
  }
  return { response, tokens, access };
}
export function userIdFromToken(token?: string): string | null {
  try { const payload = JSON.parse(Buffer.from(token?.split(".")[1] || "", "base64url").toString()); return /^\d+$/.test(String(payload.uid)) ? String(payload.uid) : null; } catch { return null; }
}
