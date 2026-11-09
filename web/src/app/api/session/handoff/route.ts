import type { NextRequest } from "next/server";
import { readJson, sessionRouteGuard, startSession } from "@/lib/server/session";

/**
 * Signs in with a one-time code from the app ("Buy on website"). The website counts as the student's browser even on
 * the phone that runs the app, so opening it from the app never asks to sign the app out.
 */
export async function POST(req: NextRequest) {
  const guard = sessionRouteGuard(req);
  if (guard) return guard;
  const input = await readJson<{ code?: string; takeOver?: boolean }>(req);
  if (input instanceof Response) return input;
  return startSession(req, "/api/v1/auth/handoff/redeem", { code: input.code ?? "", takeOver: input.takeOver === true, deviceSlot: "browser" });
}
