import createClient from "openapi-fetch";
import type { paths } from "./schema";

/** Typed client for the backend API. Calls go through the same-origin BFF proxy at /api/v1/*. */
export const api = createClient<paths>({ baseUrl: "" });

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
  const p = (body ?? {}) as Problem;
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
  const body = text ? JSON.parse(text) : undefined;
  if (!res.ok) throw toApiError(res.status, body);
  return body as T;
}

/** Human-readable message for an ApiError, any Error, or a plain validation string. */
export function errorMessage(e: unknown): string {
  if (typeof e === "string") return e;
  return e instanceof Error ? e.message : "Something went wrong.";
}
