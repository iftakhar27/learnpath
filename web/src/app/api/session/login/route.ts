import type { NextRequest } from "next/server";
import { STAFF_ROLES, problem, readJson, sessionRouteGuard, startSession } from "@/lib/server/session";

/** Email/password login. With `staffOnly`, accounts without a staff role are refused and no session is created. */
export async function POST(req: NextRequest) {
  const guard = sessionRouteGuard(req);
  if (guard) return guard;
  const input = await readJson<{ email?: string; password?: string; staffOnly?: boolean; takeOver?: boolean }>(req);
  if (input instanceof Response) return input;

  return startSession(req, "/api/v1/auth/login", { email: input.email ?? "", password: input.password ?? "", takeOver: input.takeOver === true }, (tokens) =>
    input.staffOnly && !tokens.roles.some((r) => STAFF_ROLES.includes(r))
      ? problem(403, "not_staff", "This account does not have access to the admin portal.")
      : null,
  );
}
