import { NextResponse, type NextRequest } from "next/server";
import { API_BASE_URL, REFRESH_COOKIE, clearSessionCookies, isSameOrigin, problem } from "@/lib/server/session";

export async function POST(req: NextRequest) {
  if (!isSameOrigin(req)) return problem(403, "cross_origin", "Cross-origin request rejected.");
  const refreshToken = req.cookies.get(REFRESH_COOKIE)?.value;
  if (refreshToken) {
    await fetch(`${API_BASE_URL}/api/v1/auth/logout`, {
      method: "POST",
      headers: { "content-type": "application/json" },
      body: JSON.stringify({ refreshToken }),
      cache: "no-store",
    }).catch(() => undefined);
  }
  const res = new NextResponse(null, { status: 204 });
  clearSessionCookies(res);
  return res;
}
