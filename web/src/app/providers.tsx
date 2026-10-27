"use client";

import { MutationCache, QueryCache, QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { useState, type ReactNode } from "react";
import { ApiError } from "@/lib/api/client";
import { ToastProvider } from "@/components/ui/toast";

/** A 401 anywhere means the session ended (expired, revoked, or logged out elsewhere): go back to login. */
function onAuthError(error: unknown) {
  if (error instanceof ApiError && error.status === 401 && !window.location.pathname.startsWith("/admin/login")) {
    const next = encodeURIComponent(window.location.pathname + window.location.search);
    // eslint-disable-next-line @next/next/no-location-assign-relative-destination -- full reload clears every cached query after a session change
    window.location.assign(`/admin/login?next=${next}&expired=1`);
  }
}

export function Providers({ children }: { children: ReactNode }) {
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
