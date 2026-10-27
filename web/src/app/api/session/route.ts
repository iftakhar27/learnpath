import { NextResponse, type NextRequest } from "next/server";
import { API_BASE_URL, applyAccessResult, problem, resolveAccess } from "@/lib/server/session";

import type { SessionUser } from "@/lib/session";

/** The signed-in user, read from the API so roles are always current. 401 when there is no valid session. */
export async function GET(req: NextRequest) {
  const access = await resolveAccess(req);
  if (!access.accessToken) return applyAccessResult(NextResponse.json({ status: 401, code: "unauthenticated", title: "Not signed in." }, { status: 401 }), access);

  let upstream: Response;
  try {
    upstream = await fetch(`${API_BASE_URL}/api/v1/student/profile`, {
      headers: { authorization: `Bearer ${access.accessToken}` },
      cache: "no-store",
    });
  } catch {
    return problem(502, "api_unreachable", "The server is unreachable. Please try again.");
  }
  if (!upstream.ok) return applyAccessResult(NextResponse.json({ status: 401, code: "unauthenticated", title: "Not signed in." }, { status: 401 }), access);

  const p = await upstream.json();
  const user: SessionUser = { userId: p.userId, fullName: p.fullName, email: p.email ?? null, roles: p.roles ?? [] };
  return applyAccessResult(NextResponse.json(user), access);
}
