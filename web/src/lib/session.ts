"use client";

import { useQuery, useQueryClient } from "@tanstack/react-query";
import { rawFetch } from "./api/client";

export type SessionUser = { userId: string; fullName: string; email: string | null; roles: string[] };

export function useSession() {
  return useQuery({
    queryKey: ["session"],
    queryFn: () => rawFetch<SessionUser>("/api/session"),
    staleTime: 5 * 60_000,
    retry: false,
  });
}

export function useLogout() {
  const qc = useQueryClient();
  return async () => {
    await fetch("/api/session/logout", { method: "POST" }).catch(() => undefined);
    qc.clear();
    window.location.assign("/admin/login");
  };
}

/** Mirrors the backend authorization policies; the API remains the source of truth. */
const has = (roles: readonly string[] | undefined, ...allowed: string[]) => !!roles?.some((r) => allowed.includes(r));

export const can = {
  staff: (r?: readonly string[]) => has(r, "Teacher", "ContentAdmin", "Support", "Finance", "Analyst", "SuperAdmin"),
  editContent: (r?: readonly string[]) => has(r, "Teacher", "ContentAdmin", "SuperAdmin"),
  publishContent: (r?: readonly string[]) => has(r, "ContentAdmin", "SuperAdmin"),
  support: (r?: readonly string[]) => has(r, "Support", "SuperAdmin"),
  finance: (r?: readonly string[]) => has(r, "Finance", "SuperAdmin"),
  superAdmin: (r?: readonly string[]) => has(r, "SuperAdmin"),
};
