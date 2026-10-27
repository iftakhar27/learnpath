import { NextResponse, type NextRequest } from "next/server";
import { API_BASE_URL, STAFF_ROLES, isSameOrigin, problem, setSessionCookies, type AuthTokens } from "@/lib/server/session";

/** Email/password login. With `staffOnly`, accounts without a staff role are refused and no session is created. */
export async function POST(req: NextRequest) {
  if (!isSameOrigin(req)) return problem(403, "cross_origin", "Cross-origin request rejected.");
  let input: { email?: string; password?: string; staffOnly?: boolean };
  try {
    input = await req.json();
  } catch {
    return problem(400, "invalid_request", "Invalid request.");
  }

  let upstream: Response;
  try {
    upstream = await fetch(`${API_BASE_URL}/api/v1/auth/login`, {
      method: "POST",
      headers: { "content-type": "application/json" },
      body: JSON.stringify({ email: input.email ?? "", password: input.password ?? "" }),
      cache: "no-store",
    });
  } catch {
    return problem(502, "api_unreachable", "The server is unreachable. Please try again.");
  }
  if (!upstream.ok) {
    return new NextResponse(upstream.body, { status: upstream.status, headers: { "content-type": upstream.headers.get("content-type") ?? "application/json" } });
  }

  const tokens = (await upstream.json()) as AuthTokens;
  if (input.staffOnly && !tokens.roles.some((r) => STAFF_ROLES.includes(r))) {
    // Do not leave a valid student session behind: revoke the refresh token we just received.
    await fetch(`${API_BASE_URL}/api/v1/auth/logout`, {
      method: "POST",
      headers: { "content-type": "application/json" },
      body: JSON.stringify({ refreshToken: tokens.refreshToken }),
    }).catch(() => undefined);
    return problem(403, "not_staff", "This account does not have access to the admin portal.");
  }

  const res = NextResponse.json({ userId: tokens.userId, roles: tokens.roles });
  setSessionCookies(res, tokens);
  return res;
}
