import createClient from "openapi-fetch";
import type { paths } from "./schema";
import { getLanguage } from "@/lib/i18n/current";

/** Typed client for the backend API. Calls go through the same-origin BFF proxy at /api/v1/*. */
export const api = createClient<paths>({ baseUrl: "" });

// Student pages get API content (titles, explanations, plan reasons) in the chosen language. The admin portal
// edits both languages side by side, so it keeps the default.
api.use({
  onRequest({ request }) {
    if (typeof window !== "undefined" && !window.location.pathname.startsWith("/admin")) request.headers.set("accept-language", getLanguage());
    return request;
  },
});

/** Problem-details error from the API, with the stable machine-readable `code`. */
export class ApiError extends Error {
  constructor(
    public status: number,
    public code: string,
    message: string,
    public validation?: Record<string, string[]>,
  ) {
    super(message);
  }
}

type Problem = { status?: number; code?: string; title?: string; errors?: Record<string, string[]> };

function toApiError(status: number, body: unknown): ApiError {
  const p = (body && typeof body === "object" ? body : {}) as Problem;
  // ASP.NET model validation returns { errors: { Field: [messages] } } without a code.
  const firstValidation = p.errors ? Object.values(p.errors).flat()[0] : undefined;
  const message = firstValidation ?? p.title ?? (status >= 500 ? "Something went wrong. Please try again." : `Request failed (${status}).`);
  return new ApiError(status, p.code ?? (p.errors ? "validation_failed" : "http_" + status), message, p.errors);
}

/** Unwraps an openapi-fetch result: returns data, or throws ApiError for any non-2xx response. */
export async function unwrap<T>(call: Promise<{ data?: T; error?: unknown; response: Response }>): Promise<T> {
  let result: { data?: T; error?: unknown; response: Response };
  try {
    result = await call;
  } catch {
    throw new ApiError(0, "network_error", "Network error. Check your connection and try again.");
  }
  if (!result.response.ok) throw toApiError(result.response.status, result.error);
  return result.data as T;
}

/** For endpoints openapi-fetch cannot express well (multipart uploads, downloads). */
export async function rawFetch<T>(input: string, init?: RequestInit): Promise<T> {
  let res: Response;
  try {
    res = await fetch(input, init);
  } catch {
    throw new ApiError(0, "network_error", "Network error. Check your connection and try again.");
  }
  const text = await res.text();
  let body: unknown;
  try {
    body = text ? JSON.parse(text) : undefined;
  } catch {
    // Not JSON (e.g. a hosting error page): never show a parser message to the user.
    throw new ApiError(res.status, "bad_response", res.status >= 500 || res.ok ? "The server is unavailable right now. Please try again in a few seconds." : `Request failed (${res.status}).`);
  }
  if (!res.ok) throw toApiError(res.status, body);
  return body as T;
}

/** Human-readable message for an ApiError, any Error, or a plain validation string. */
export function errorMessage(e: unknown): string {
  if (typeof e === "string") return e;
  return e instanceof Error ? e.message : "Something went wrong.";
}
