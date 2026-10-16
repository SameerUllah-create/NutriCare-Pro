import { NextRequest, NextResponse } from "next/server";
import { authorizedFetch, clearSession, failure, sameOrigin, setSession } from "@/lib/session.server";

async function handle(request: NextRequest, { params }: { params: Promise<{ path: string[] }> }) {
  if (!["GET", "HEAD"].includes(request.method) && !sameOrigin(request)) return failure("Invalid request origin.", 403);
  const { path } = await params;
  if (path[0]?.toLowerCase() !== "api" || path.some(part => part === "." || part === ".." || /[\\/]/.test(part))) return failure("Unknown API resource.", 404);
  if (path[1]?.toLowerCase() === "auth" && ["login", "register", "refresh-token", "logout", "reset-password", "forgot-password", "verify-email", "resend-verification"].includes(path[2]?.toLowerCase())) return failure("Use the account endpoint for this action.", 403);
  try {
    const headers = new Headers();
    for (const name of ["content-type", "idempotency-key", "if-match"]) { const value = request.headers.get(name); if (value) headers.set(name, value); }
    const body = ["GET", "HEAD"].includes(request.method) ? undefined : await request.arrayBuffer();
    const result = await authorizedFetch(request, `/${path.map(encodeURIComponent).join("/")}${request.nextUrl.search}`, { method: request.method, headers, body });
    const response = new NextResponse(result.response.body, { status: result.response.status });
    for (const name of ["content-type", "content-disposition", "x-request-id", "etag"]) { const value = result.response.headers.get(name); if (value) response.headers.set(name, value); }
    response.headers.set("Cache-Control", "no-store");
    if (result.tokens) setSession(response, result.tokens);
    if (result.response.status === 401) clearSession(response);
    return response;
  } catch { return failure("The service is unavailable. Please retry in a moment.", 503); }
}
export { handle as GET, handle as POST, handle as PUT, handle as PATCH, handle as DELETE };
