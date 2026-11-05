"use client";

import { useEffect } from "react";

/**
 * On free hosting the API sleeps when idle and takes 25–60 s to wake. Sign-in pages call this so the API starts
 * waking while the visitor types; the sign-in request then usually finds it ready.
 */
export function useWakeApi() {
  useEffect(() => {
    void fetch("/api/v1/config", { cache: "no-store" }).catch(() => undefined);
  }, []);
}
