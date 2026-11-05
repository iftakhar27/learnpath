"use client";

import { MutationCache, QueryCache, QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { useState, type ReactNode } from "react";
import { ApiError } from "@/lib/api/client";
import { ToastProvider } from "@/components/ui/toast";
import { useWakeApi } from "@/lib/api/warm-up";

const PUBLIC_PATHS = ["/", "/login", "/register", "/admin/login"];

/** A 401 anywhere means the session ended (expired, revoked, or logged out elsewhere): go back to that area's login. */
function onAuthError(error: unknown) {
  const { pathname, search } = window.location;
  if (error instanceof ApiError && error.status === 401 && !PUBLIC_PATHS.includes(pathname)) {
    const login = pathname.startsWith("/admin") ? "/admin/login" : "/login";
    // eslint-disable-next-line @next/next/no-location-assign-relative-destination -- full reload clears every cached query after a session change
    window.location.assign(`${login}?next=${encodeURIComponent(pathname + search)}&expired=1`);
  }
}

export function Providers({ children }: { children: ReactNode }) {
  useWakeApi();
  const [client] = useState(
    () =>
      new QueryClient({
        queryCache: new QueryCache({ onError: onAuthError }),
        mutationCache: new MutationCache({ onError: onAuthError }),
        defaultOptions: {
          queries: {
            staleTime: 30_000,
            refetchOnWindowFocus: false,
            retry: (count, error) => !(error instanceof ApiError && error.status >= 400 && error.status < 500) && count < 2,
          },
        },
      }),
  );
  return (
    <QueryClientProvider client={client}>
      <ToastProvider>{children}</ToastProvider>
    </QueryClientProvider>
  );
}
