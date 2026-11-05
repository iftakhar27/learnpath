"use client";

import { useState } from "react";
import { useQuery } from "@tanstack/react-query";
import { api, unwrap } from "@/lib/api/client";
import { useT, type MessageKey } from "@/lib/i18n";
import { Button } from "@/components/ui/button";
import { Modal } from "@/components/ui/dialog";
import { PageLoading } from "@/components/ui/feedback";
import { Textarea } from "@/components/ui/field";
import { useToast } from "@/components/ui/toast";
import { StudentError } from "../errors";
import { Muted, PageTitle, useFormat } from "../ui";

const STATUS: Record<string, { label: MessageKey; tone: string }> = {
  Paid: { label: "orderPaid", tone: "bg-success-soft text-success" },
  Created: { label: "orderPending", tone: "bg-surface-muted text-fg-muted" },
  Failed: { label: "orderFailed", tone: "bg-danger-soft text-danger" },
  Refunded: { label: "orderRefunded", tone: "bg-warning-soft text-warning" },
};

export function PurchasesPage() {
  const t = useT();
  const f = useFormat();
  const toast = useToast();
  const q = useQuery({ queryKey: ["student", "orders"], queryFn: () => unwrap(api.GET("/api/v1/payments/orders")) });
  const [refundFor, setRefundFor] = useState<string | null>(null);
  const [reason, setReason] = useState("");
  const [sending, setSending] = useState(false);
  const [error, setError] = useState<unknown>(null);

  const sendRefund = async () => {
    if (!refundFor || !reason.trim()) return;
    setSending(true);
    setError(null);
    try {
      await unwrap(api.POST("/api/v1/payments/orders/{orderId}/refund-request", { params: { path: { orderId: refundFor } }, body: { reason: reason.trim() } }));
      setRefundFor(null);
      setReason("");
      toast(t("refundRequested"));
    } catch (e) {
      setError(e);
    } finally {
      setSending(false);
    }
  };

  if (q.isPending) return <PageLoading />;
  if (q.isError) return <StudentError error={q.error} onRetry={() => q.refetch()} />;
  return (
    <div className="mx-auto max-w-2xl">
      <PageTitle>{t("myPurchases")}</PageTitle>
      {q.data.length === 0 ? (
        <Muted>{t("ordersEmpty")}</Muted>
      ) : (
        <ul className="space-y-3">
          {q.data.map((o) => {
            const status = STATUS[o.status] ?? STATUS.Created;
            return (
              <li key={o.orderId} className="flex flex-wrap items-center gap-3 rounded-2xl border border-border bg-surface p-4">
                <span className="min-w-0 flex-1">
                  <span className="block font-medium">{o.courseTitle}</span>
                  <span className="block text-sm text-fg-muted">
                    {f.rupees(o.amountInPaise)} · {f.date(o.paidAt ?? o.createdAt)}
                  </span>
                </span>
                <span className={`rounded-full px-2.5 py-0.5 text-xs font-medium ${status.tone}`}>{t(status.label)}</span>
                {o.status === "Paid" && (
                  <Button size="sm" variant="ghost" onClick={() => setRefundFor(o.orderId)}>
                    {t("requestRefund")}
                  </Button>
                )}
              </li>
            );
          })}
        </ul>
      )}
      <Modal
        open={!!refundFor}
        onClose={() => setRefundFor(null)}
        title={t("requestRefund")}
        closeLabel={t("cancel")}
        footer={
          <>
            <Button variant="secondary" onClick={() => setRefundFor(null)}>
              {t("cancel")}
            </Button>
            <Button onClick={sendRefund} loading={sending} disabled={!reason.trim()}>
              {t("send")}
            </Button>
          </>
        }
      >
        <div className="space-y-3">
          <StudentError error={error} />
          <label className="block space-y-1">
            <span className="text-sm font-medium">{t("refundReason")}</span>
            <Textarea value={reason} onChange={(e) => setReason(e.target.value)} maxLength={1000} rows={4} />
          </label>
        </div>
      </Modal>
    </div>
  );
}
