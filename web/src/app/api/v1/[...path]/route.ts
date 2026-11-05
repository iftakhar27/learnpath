import { NextResponse, type NextRequest } from "next/server";
import { apiFetch, apiNotConfigured, apiStarting, applyAccessResult, isEdgeError, isSameOrigin, problem, resolveAccess, visitorHeaders } from "@/lib/server/session";

/**
 * Forwards /api/v1/* from the browser to the backend API with the session's bearer token.
 * Token-issuing endpoints are blocked here so tokens never reach browser JavaScript; use /api/session/* instead.
 */
const BLOCKED_PREFIXES = [["auth"], ["payments", "webhooks"]];
const FORWARDED_REQUEST_HEADERS = ["content-type", "accept", "accept-language"];
const FORWARDED_RESPONSE_HEADERS = ["content-type", "content-disposition", "cache-control"];

async function forward(req: NextRequest, ctx: RouteContext<"/api/v1/[...path]">) {
  const { path } = await ctx.params;
  if (BLOCKED_PREFIXES.some((p) => p.every((seg, i) => path[i]?.toLowerCase() === seg))) {
    return problem(404, "not_found", "Not found.");
  }
  if (req.method !== "GET" && req.method !== "HEAD" && !isSameOrigin(req)) {
    return problem(403, "cross_origin", "Cross-origin request rejected.");
  }

  const notConfigured = apiNotConfigured();
  if (notConfigured) return notConfigured;

  const apiPath = `/api/v1/${path.map(encodeURIComponent).join("/")}${req.nextUrl.search}`;
  // POSTs aren't repeated (not all are idempotent); reads and PUT/DELETE are.
  const retryEdge = req.method !== "POST";
  const body = req.method === "GET" || req.method === "HEAD" ? undefined : await req.arrayBuffer();

  const send = (token: string | null) => {
    const headers = new Headers(visitorHeaders(req));
    for (const h of FORWARDED_REQUEST_HEADERS) {
      const v = req.headers.get(h);
      if (v) headers.set(h, v);
    }
    if (token) headers.set("authorization", `Bearer ${token}`);
    return apiFetch(apiPath, { method: req.method, headers, body }, retryEdge);
  };

  let access = await resolveAccess(req);
  if (access.unavailable) return apiStarting();
  let upstream: Response;
  try {
    upstream = await send(access.accessToken);
    // The access token can be revoked or rotated server-side before it expires; retry once with a fresh one.
    if (upstream.status === 401 && access.accessToken && !access.refreshed) {
      const retry = await resolveAccess(req, true);
      if (retry.accessToken) {
        access = retry;
        upstream = await send(retry.accessToken);
      } else {
        access = retry;
      }
    }
  } catch {
    return problem(502, "api_unreachable", "The server is unreachable. Please try again.");
  }
  if (access.unavailable) return apiStarting();
  if (isEdgeError(upstream)) return applyAccessResult(apiStarting(), access);

  const headers = new Headers();
  for (const h of FORWARDED_RESPONSE_HEADERS) {
    const v = upstream.headers.get(h);
    if (v) headers.set(h, v);
  }
  const res = new NextResponse(upstream.status === 204 || upstream.status === 304 ? null : upstream.body, { status: upstream.status, headers });
  return applyAccessResult(res, access);
}

export const GET = forward;
export const POST = forward;
export const PUT = forward;
export const PATCH = forward;
export const DELETE = forward;
