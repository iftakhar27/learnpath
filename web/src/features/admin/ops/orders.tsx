"use client";

import Link from "next/link";
import { useState } from "react";
import { keepPreviousData, useQuery } from "@tanstack/react-query";
import { api, unwrap } from "@/lib/api/client";
import { ORDER_STATUSES, type OrderStatus } from "@/lib/api/types";
import { formatDateTime, formatPaise } from "@/lib/format";
import { can, useSession } from "@/lib/session";
import { EmptyState, ErrorState, SkeletonRows, StatusBadge } from "@/components/ui/feedback";
import { Select } from "@/components/ui/field";
import { Card, PageHeader, Pagination, Table, Td, Th } from "@/components/ui/layout";

export function Orders() {
  const { data: me } = useSession();
  const [status, setStatus] = useState<OrderStatus | "">("");
  const [page, setPage] = useState(1);
  const orders = useQuery({
    queryKey: ["admin", "orders", { status, page }],
    placeholderData: keepPreviousData,
    queryFn: () => unwrap(api.GET("/api/v1/admin/orders", { params: { query: { status: status || undefined, page } } })),
  });

  return (
    <>
      <PageHeader
        title="Orders"
        description="Payments are confirmed only by Razorpay's signed callback or webhook. Amounts are GST-inclusive. Refund requests arrive as high-priority support tickets."
      />
      <Card
        title="All orders"
        actions={
          <Select aria-label="Filter by status" className="h-8 w-40" value={status} onChange={(e) => { setStatus(e.target.value as OrderStatus | ""); setPage(1); }}>
            <option value="">Any status</option>
            {ORDER_STATUSES.map((s) => (
              <option key={s}>{s}</option>
            ))}
          </Select>
        }
      >
        {orders.isPending ? (
          <SkeletonRows />
        ) : orders.isError ? (
          <ErrorState error={orders.error} onRetry={() => orders.refetch()} />
        ) : orders.data.items.length === 0 ? (
          <EmptyState title="No orders yet" />
        ) : (
          <>
            <Table>
              <thead>
                <tr>
                  <Th>Student</Th>
                  <Th>Course</Th>
                  <Th className="text-right">Amount</Th>
                  <Th>Status</Th>
                  <Th>Receipt / gateway order</Th>
                  <Th>Created</Th>
                  <Th>Paid</Th>
                </tr>
              </thead>
              <tbody className={orders.isPlaceholderData ? "opacity-60" : ""}>
                {orders.data.items.map((o) => (
                  <tr key={o.id}>
                    <Td>{can.support(me?.roles) ? <Link className="text-primary hover:underline" href={`/admin/students/${o.userId}`}>{o.userName}</Link> : o.userName}</Td>
                    <Td>{o.courseTitle}</Td>
                    <Td className="text-right tabular-nums">{formatPaise(o.amountInPaise)}</Td>
                    <Td>
                      <StatusBadge status={o.status} />
                    </Td>
                    <Td className="font-mono text-xs">
                      {o.receipt}
                      <div className="text-fg-muted">{o.gatewayOrderId}</div>
                    </Td>
                    <Td className="whitespace-nowrap text-fg-muted">{formatDateTime(o.createdAt)}</Td>
                    <Td className="whitespace-nowrap text-fg-muted">{formatDateTime(o.paidAt)}</Td>
                  </tr>
                ))}
              </tbody>
            </Table>
            <Pagination page={orders.data.page} pageSize={orders.data.pageSize} total={orders.data.total} onPage={setPage} />
          </>
        )}
      </Card>
    </>
  );
}
