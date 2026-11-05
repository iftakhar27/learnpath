import type { NextRequest } from "next/server";
import { readJson, sessionRouteGuard, startSession } from "@/lib/server/session";

/** Creates a student account with email and password and signs it in. */
export async function POST(req: NextRequest) {
  const guard = sessionRouteGuard(req);
  if (guard) return guard;
  const input = await readJson<{ email?: string; password?: string; fullName?: string; acceptTerms?: boolean }>(req);
  if (input instanceof Response) return input;
  return startSession(req, "/api/v1/auth/register", {
    email: input.email ?? "",
    password: input.password ?? "",
    fullName: input.fullName ?? "",
    acceptTerms: input.acceptTerms === true,
  });
}
