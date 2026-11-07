import { NextResponse, type NextRequest } from "next/server";
import { apiFetch, apiStarting, applyAccessResult, isEdgeError, problem, resolveAccess, sessionReplaced } from "@/lib/server/session";

import type { SessionUser } from "@/lib/session";

/** The signed-in user, read from the API so roles are always current. 401 when there is no valid session. */
export async function GET(req: NextRequest) {
  const access = await resolveAccess(req);
  if (access.unavailable) return apiStarting();
  if (access.reason === "session_replaced") return sessionReplaced(access);
  if (!access.accessToken) return applyAccessResult(NextResponse.json({ status: 401, code: "unauthenticated", title: "Not signed in." }, { status: 401 }), access);

  let upstream: Response;
  try {
    upstream = await apiFetch("/api/v1/student/profile", { headers: { authorization: `Bearer ${access.accessToken}` } });
  } catch {
    return problem(502, "api_unreachable", "The server is unreachable. Please try again.");
  }
  if (isEdgeError(upstream) || upstream.status >= 500) return apiStarting();
  if (!upstream.ok) return applyAccessResult(NextResponse.json({ status: 401, code: "unauthenticated", title: "Not signed in." }, { status: 401 }), access);

  const p = await upstream.json();
  const user: SessionUser = { userId: p.userId, fullName: p.fullName, email: p.email ?? null, roles: p.roles ?? [] };
  return applyAccessResult(NextResponse.json(user), access);
}
