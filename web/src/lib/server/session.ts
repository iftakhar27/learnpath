import "server-only";
import { NextResponse, type NextRequest } from "next/server";

/**
 * Backend-for-frontend session handling.
 *
 * The API issues a short-lived JWT and a rotating refresh token. Both live in httpOnly cookies set by this
 * Next.js server, so browser JavaScript never sees a token. Every API call from the browser goes through
 * /api/v1/* (see app/api/v1/[...path]/route.ts), which attaches the access token and refreshes it when needed.
 */

const configuredApiUrl = process.env.API_BASE_URL?.trim();
export const API_BASE_URL = (configuredApiUrl || "http://localhost:5080").replace(/\/$/, "");
export const ACCESS_COOKIE = "lp_at";
export const REFRESH_COOKIE = "lp_rt";
const REFRESH_COOKIE_DAYS = 30;

const BFF_SHARED_KEY = process.env.BFF_SHARED_KEY?.trim();
/** Request header that carries the visitor's address as set by the hosting edge (true-client-ip on Render by default). */
const CLIENT_IP_HEADER = (process.env.CLIENT_IP_HEADER?.trim() || (process.env.RENDER === "true" ? "true-client-ip" : "")).toLowerCase();

/**
 * Every API call from this server would otherwise come from one address, so all visitors would share the API's
 * per-client limits (sign-in attempts). With a shared key configured on both servers, pass the visitor's address on.
 */
export function visitorHeaders(req: NextRequest): Record<string, string> {
  if (!BFF_SHARED_KEY || !CLIENT_IP_HEADER) return {};
  const ip = req.headers.get(CLIENT_IP_HEADER)?.trim();
  return ip ? { "x-learnpath-bff-key": BFF_SHARED_KEY, "x-learnpath-client-ip": ip } : {};
}
const secureCookies = process.env.COOKIE_SECURE ? process.env.COOKIE_SECURE === "true" : process.env.NODE_ENV === "production";

export type AuthTokens = {
  accessToken: string;
  accessTokenExpiresAt: string;
  refreshToken: string;
  userId: string;
  roles: string[];
  onboardingComplete: boolean;
};

export const STAFF_ROLES = ["Teacher", "ContentAdmin", "Support", "Finance", "Analyst", "SuperAdmin"];

/** Seconds since epoch at which the JWT expires, read without verification (the API verifies it). */
function jwtExpiry(token: string): number {
  try {
    const payload = JSON.parse(Buffer.from(token.split(".")[1], "base64url").toString("utf8"));
    return typeof payload.exp === "number" ? payload.exp : 0;
  } catch {
    return 0;
  }
}

export function setSessionCookies(res: NextResponse, tokens: AuthTokens) {
  const common = { httpOnly: true, secure: secureCookies, sameSite: "lax" as const, path: "/" };
  const accessMaxAge = Math.max(60, jwtExpiry(tokens.accessToken) - Math.floor(Date.now() / 1000));
  res.cookies.set(ACCESS_COOKIE, tokens.accessToken, { ...common, maxAge: accessMaxAge });
  res.cookies.set(REFRESH_COOKIE, tokens.refreshToken, { ...common, maxAge: REFRESH_COOKIE_DAYS * 24 * 3600 });
}

export function clearSessionCookies(res: NextResponse) {
  res.cookies.delete(ACCESS_COOKIE);
  res.cookies.delete(REFRESH_COOKIE);
}

/**
 * The API revokes every session when a rotated refresh token is presented again (theft detection), so parallel
 * requests that all find an expired access token must share one refresh call. Results are kept briefly so requests
 * that still carry the old cookie (sent before the browser stored the new one) reuse the new tokens.
 * This is per server process: run a single Next.js instance or use sticky sessions.
 */
const refreshes = new Map<string, { promise: Promise<AuthTokens | null>; expires: number }>();
const REFRESH_REUSE_MS = 60_000;

export function refreshSession(refreshToken: string, extraHeaders: Record<string, string> = {}): Promise<AuthTokens | null> {
  const now = Date.now();
  for (const [key, entry] of refreshes) if (entry.expires < now) refreshes.delete(key);
  const existing = refreshes.get(refreshToken);
  if (existing) return existing.promise;

  const promise = fetch(`${API_BASE_URL}/api/v1/auth/refresh`, {
    method: "POST",
    headers: { "content-type": "application/json", ...extraHeaders },
    body: JSON.stringify({ refreshToken }),
    cache: "no-store",
  })
    .then(async (r) => (r.ok ? ((await r.json()) as AuthTokens) : null))
    .catch(() => null);
  refreshes.set(refreshToken, { promise, expires: now + REFRESH_REUSE_MS });
  return promise;
}

export type ResolvedAccess = {
  accessToken: string | null;
  /** Set when this request refreshed the session; the caller must write the new cookies. */
  refreshed: AuthTokens | null;
  /** True when a refresh token existed but the API rejected it; the caller should clear cookies. */
  expired: boolean;
};

/** Returns a usable access token, refreshing when the current one is missing or about to expire. */
export async function resolveAccess(req: NextRequest, forceRefresh = false): Promise<ResolvedAccess> {
  const access = req.cookies.get(ACCESS_COOKIE)?.value;
  const refresh = req.cookies.get(REFRESH_COOKIE)?.value;
  if (!forceRefresh && access && jwtExpiry(access) - 30 > Date.now() / 1000) {
    return { accessToken: access, refreshed: null, expired: false };
  }
  if (!refresh) return { accessToken: null, refreshed: null, expired: false };
  const tokens = await refreshSession(refresh, visitorHeaders(req));
  return tokens
    ? { accessToken: tokens.accessToken, refreshed: tokens, expired: false }
    : { accessToken: null, refreshed: null, expired: true };
}

export function applyAccessResult(res: NextResponse, result: ResolvedAccess) {
  if (result.refreshed) setSessionCookies(res, result.refreshed);
  else if (result.expired) clearSessionCookies(res);
  return res;
}

/** Rejects cross-site state-changing requests (defence in depth on top of SameSite=Lax cookies). */
export function isSameOrigin(req: NextRequest): boolean {
  const origin = req.headers.get("origin");
  if (!origin) return true; // same-origin fetches from older browsers and server-side callers
  const host = req.headers.get("x-forwarded-host") ?? req.headers.get("host");
  try {
    return new URL(origin).host === host;
  } catch {
    return false;
  }
}

/**
 * In production the API address must be set explicitly. Falling back to localhost there would only ever produce a
 * vague "unreachable" error, so routes answer with this instead.
 */
export function apiNotConfigured(): Response | null {
  if (configuredApiUrl || process.env.NODE_ENV !== "production") return null;
  return problem(503, "api_not_configured", "This website isn't connected to the API yet: set API_BASE_URL on the web server.");
}

export function problem(status: number, code: string, title: string) {
  return Response.json({ status, code, title }, { status });
}

/** Fields of the token response the browser may see (never the tokens themselves). */
export type SessionStart = { userId: string; roles: string[]; onboardingComplete: boolean };

/**
 * Calls a token-issuing API endpoint (login, register, OTP verify) on the visitor's behalf. On success the tokens go
 * into httpOnly cookies and only the session summary reaches the browser; API errors are passed through unchanged.
 * `accept` can refuse the account (e.g. students on the admin login) after the API has issued tokens.
 */
export async function startSession(
  req: NextRequest,
  apiPath: string,
  body: unknown,
  accept?: (tokens: AuthTokens) => Response | null,
): Promise<Response> {
  let upstream: Response;
  try {
    upstream = await fetch(`${API_BASE_URL}${apiPath}`, {
      method: "POST",
      headers: { "content-type": "application/json", ...visitorHeaders(req) },
      body: JSON.stringify(body),
      cache: "no-store",
    });
  } catch {
    return problem(502, "api_unreachable", "The server is unreachable. Please try again.");
  }
  if (!upstream.ok) {
    return new Response(upstream.body, { status: upstream.status, headers: { "content-type": upstream.headers.get("content-type") ?? "application/json" } });
  }
  const tokens = (await upstream.json()) as AuthTokens;
  const refused = accept?.(tokens);
  if (refused) {
    // Do not leave a valid session behind: revoke the refresh token we just received.
    await fetch(`${API_BASE_URL}/api/v1/auth/logout`, {
      method: "POST",
      headers: { "content-type": "application/json" },
      body: JSON.stringify({ refreshToken: tokens.refreshToken }),
    }).catch(() => undefined);
    return refused;
  }
  const summary: SessionStart = { userId: tokens.userId, roles: tokens.roles, onboardingComplete: tokens.onboardingComplete };
  const res = NextResponse.json(summary);
  setSessionCookies(res, tokens);
  return res;
}

/** Parses a JSON request body, or returns a 400 problem. */
export async function readJson<T>(req: NextRequest): Promise<T | Response> {
  try {
    return (await req.json()) as T;
  } catch {
    return problem(400, "invalid_request", "Invalid request.");
  }
}

/** Common guards for session routes: same-origin and API configured. */
export function sessionRouteGuard(req: NextRequest): Response | null {
  if (!isSameOrigin(req)) return problem(403, "cross_origin", "Cross-origin request rejected.");
  return apiNotConfigured();
}
