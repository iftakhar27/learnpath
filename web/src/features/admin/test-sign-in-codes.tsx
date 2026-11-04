"use client";

import { useEffect, useState } from "react";
import { useQuery } from "@tanstack/react-query";
import { ApiError, api, unwrap } from "@/lib/api/client";
import { formatDateTime } from "@/lib/format";
import { Card, Table, Td, Th } from "@/components/ui/layout";
import { Badge, EmptyState, InlineError } from "@/components/ui/feedback";

/** Re-renders every second while `active`, so countdowns stay current. */
function useNow(active: boolean) {
  const [now, setNow] = useState(() => Date.now());
  useEffect(() => {
    if (!active) return;
    const id = setInterval(() => setNow(Date.now()), 1000);
    return () => clearInterval(id);
  }, [active]);
  return now;
}

function remaining(ms: number) {
  const s = Math.max(0, Math.round(ms / 1000));
  return `${Math.floor(s / 60)}:${String(s % 60).padStart(2, "0")}`;
}

/**
 * Test servers don't send SMS, so the sign-in codes people request in the app are shown here instead. Where a real
 * SMS provider is configured the API answers 404 and this panel doesn't render at all.
 */
export function TestSignInCodes() {
  const q = useQuery({
    queryKey: ["admin", "test-otp-codes"],
    queryFn: () => unwrap(api.GET("/api/v1/admin/test-otp-codes")),
    refetchInterval: (query) => (query.state.error ? false : 5000),
    retry: false,
  });
  const codes = q.data ?? [];
  const now = useNow(codes.length > 0);

  if (q.isPending || (q.error instanceof ApiError && q.error.status === 404)) return null;

  return (
    <Card title="Test sign-in codes" className="mb-6">
      <p className="px-4 pt-3 pb-1 text-sm text-fg-muted">
        This test server doesn&apos;t send SMS. When someone chooses <strong>Continue with mobile number</strong> in the app, their code
        appears here within a few seconds. Each code works once, for 5 minutes.
      </p>
      {q.isError ? (
        <div className="p-4">
          <InlineError error={q.error} />
        </div>
      ) : codes.length === 0 ? (
        <EmptyState title="No codes in the last hour">Request a code in the app and it will show up here.</EmptyState>
      ) : (
        <Table>
          <thead>
            <tr>
              <Th>Mobile number</Th>
              <Th>Code</Th>
              <Th>Sent</Th>
              <Th>Status</Th>
            </tr>
          </thead>
          <tbody>
            {codes.map((c) => {
              const left = new Date(c.expiresAt).getTime() - now;
              return (
                <tr key={`${c.phone}-${c.sentAt}`}>
                  <Td className="tabular-nums">{c.phone}</Td>
                  <Td>
                    <span className="select-all font-mono text-lg font-semibold tracking-widest">
                      {c.code}
                    </span>
                  </Td>
                  <Td>{formatDateTime(c.sentAt)}</Td>
                  <Td>{left > 0 ? <Badge tone="success">Valid for {remaining(left)}</Badge> : <Badge>Expired</Badge>}</Td>
                </tr>
              );
            })}
          </tbody>
        </Table>
      )}
    </Card>
  );
}
