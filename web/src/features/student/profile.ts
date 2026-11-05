"use client";

import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { api, unwrap } from "@/lib/api/client";
import type { components } from "@/lib/api/schema";
import { useLanguage, type Lang } from "@/lib/i18n";

export type Profile = components["schemas"]["ProfileDto"];
export type ProfileUpdate = components["schemas"]["UpdateProfileRequest"];

export const profileKey = ["student", "profile"] as const;

export function useProfile() {
  return useQuery({ queryKey: profileKey, queryFn: () => unwrap(api.GET("/api/v1/student/profile")), staleTime: 5 * 60_000 });
}

export function useUpdateProfile() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: (body: ProfileUpdate) => unwrap(api.PUT("/api/v1/student/profile", { body })),
    onSuccess: (profile) => qc.setQueryData(profileKey, profile),
  });
}

/** Changes the UI language and, for signed-in students, saves it to their profile (the app uses it too). */
export function useChangeLanguage() {
  const { setLang } = useLanguage();
  const update = useUpdateProfile();
  const qc = useQueryClient();
  return (lang: Lang) => {
    setLang(lang);
    const profile = qc.getQueryData<Profile>(profileKey);
    if (profile && profile.language !== lang) {
      qc.setQueryData(profileKey, { ...profile, language: lang });
      update.mutate({ language: lang });
    }
  };
}

/** Only internal paths, never another site or the admin area. */
export function safeNext(next: string | null | undefined, fallback = "/home") {
  return next && next.startsWith("/") && !next.startsWith("//") && !next.startsWith("/admin") ? next : fallback;
}
