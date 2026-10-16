import { NextRequest, NextResponse } from "next/server";
import { ACCESS_COOKIE, REFRESH_COOKIE, authorizedFetch, clearSession, failure, setSession, userIdFromToken } from "@/lib/session.server";
export async function GET(request: NextRequest) {
  if (!request.cookies.has(ACCESS_COOKIE) && !request.cookies.has(REFRESH_COOKIE)) return failure("Please sign in.", 401);
  try {
    const id = userIdFromToken(request.cookies.get(ACCESS_COOKIE)?.value);
    if (!id) { const response = failure("Please sign in again.", 401); clearSession(response); return response; }
    const result = await authorizedFetch(request, `/api/Auth/${id}`);
    const response = NextResponse.json(await result.response.json(), { status: result.response.status, headers: { "Cache-Control": "no-store" } });
    if (result.tokens) setSession(response, result.tokens);
    if (result.response.status === 401) clearSession(response);
    return response;
  } catch { return failure("The account service is unavailable.", 503); }
}
