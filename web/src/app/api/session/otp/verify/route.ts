import type { NextRequest } from "next/server";
import { readJson, sessionRouteGuard, startSession } from "@/lib/server/session";

/** Signs in (or creates an account) with a mobile number and the code sent to it. */
export async function POST(req: NextRequest) {
  const guard = sessionRouteGuard(req);
  if (guard) return guard;
  const input = await readJson<{ phone?: string; code?: string; fullName?: string; acceptTerms?: boolean }>(req);
  if (input instanceof Response) return input;
  return startSession(req, "/api/v1/auth/otp/verify", {
    phone: input.phone ?? "",
    code: input.code ?? "",
    fullName: input.fullName || null,
    acceptTerms: input.acceptTerms === true,
  });
}
