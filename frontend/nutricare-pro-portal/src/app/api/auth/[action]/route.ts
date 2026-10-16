import { NextRequest, NextResponse } from "next/server";
import { authorizedFetch, clearSession, failure, REFRESH_COOKIE, sameOrigin, setSession, upstream } from "@/lib/session.server";
import type { ApiEnvelope, Tokens, User } from "@/lib/types";

const actions = new Set(["login", "register", "forgot-password", "reset-password", "resend-verification", "verify-email", "logout"]);
export async function POST(request: NextRequest, { params }: { params: Promise<{ action: string }> }) {
  if (!sameOrigin(request)) return failure("Invalid request origin.", 403);
  const { action } = await params;
  if (!actions.has(action)) return failure("Unknown account action.", 404);
  try {
    const body = await request.json().catch(() => ({}));
    if (action === "logout") {
      const result = await authorizedFetch(request, "/api/Auth/logout", { method: "POST", headers: { "Content-Type": "application/json" }, body: JSON.stringify({ refreshToken: request.cookies.get(REFRESH_COOKIE)?.value }) });
      if (!result.response.ok && result.response.status !== 401) return failure("Sign out could not be confirmed. Please retry.", 502);
      const response = NextResponse.json({ success: true, data: null }); clearSession(response); return response;
    }
    const path = action === "verify-email" ? `/api/Auth/verify-email?${new URLSearchParams({ userId: String(body.userId), token: String(body.token) })}` : `/api/Auth/${action}`;
    const backend = await upstream(path, { method: action === "verify-email" ? "GET" : "POST", headers: { "Content-Type": "application/json" }, ...(action === "verify-email" ? {} : { body: JSON.stringify(action === "register" ? { ...body, role: "Patient" } : body) }) });
    const payload = await backend.json() as ApiEnvelope<Tokens & { user: User }>;
    if (action === "login" && backend.ok && payload.success) {
      if (!payload.data?.accessToken || !payload.data?.refreshToken) return failure("The account service returned an invalid session.", 502);
      const response = NextResponse.json({ success: true, data: payload.data.user }); setSession(response, payload.data); response.headers.set("Cache-Control", "no-store"); return response;
    }
    return NextResponse.json(payload, { status: backend.status, headers: { "Cache-Control": "no-store" } });
  } catch { return failure("The account service is unavailable. Please try again.", 503); }
}
