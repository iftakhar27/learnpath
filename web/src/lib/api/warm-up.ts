"use client";

import { useEffect } from "react";

let woken = false;

/**
 * On free hosting the API sleeps when idle and takes 25–60 s to wake, and only outside requests wake it (not the web
 * server's own calls). So once per page load the browser pings the API's health check directly; the web server's
 * calls retry for up to a minute and find it awake. `no-cors`: the response isn't read, so no CORS setup is needed.
 */
export function useWakeApi() {
  useEffect(() => {
    if (woken) return;
    woken = true;
    void fetch("/api/wake", { cache: "no-store" })
      .then((r) => r.json() as Promise<{ url: string }>)
      .then(({ url }) => fetch(url, { mode: "no-cors", cache: "no-store" }))
      .catch(() => undefined);
  }, []);
}
