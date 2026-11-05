import type { NextRequest } from "next/server";
import { API_BASE_URL, problem, readJson, sessionRouteGuard, visitorHeaders } from "@/lib/server/session";

/** Asks the API to send a sign-in code to a mobile number. No tokens are involved. */
export async function POST(req: NextRequest) {
  const guard = sessionRouteGuard(req);
  if (guard) return guard;
  const input = await readJson<{ phone?: string }>(req);
  if (input instanceof Response) return input;
  try {
    const upstream = await fetch(`${API_BASE_URL}/api/v1/auth/otp/request`, {
      method: "POST",
      headers: { "content-type": "application/json", ...visitorHeaders(req) },
      body: JSON.stringify({ phone: input.phone ?? "" }),
      cache: "no-store",
    });
    return new Response(upstream.body, { status: upstream.status, headers: { "content-type": upstream.headers.get("content-type") ?? "application/json" } });
  } catch {
    return problem(502, "api_unreachable", "The server is unreachable. Please try again.");
  }
}
