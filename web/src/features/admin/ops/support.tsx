"use client";

import Link from "next/link";
import { useState } from "react";
import { useQuery } from "@tanstack/react-query";
import { api, unwrap } from "@/lib/api/client";
import type { AdminTicket } from "@/lib/api/types";
import { formatDateTime, truncate } from "@/lib/format";
import { can, useSession } from "@/lib/session";
import { Button } from "@/components/ui/button";
import { Modal } from "@/components/ui/dialog";
import { Badge, EmptyState, ErrorState, SkeletonRows, StatusBadge } from "@/components/ui/feedback";
import { Field, Select, Textarea } from "@/components/ui/field";
import { Card, PageHeader, Tabs } from "@/components/ui/layout";
import { useAction } from "../shared";

const TICKET_FILTERS = ["Open", "InProgress", "Resolved", "Closed", ""] as const;
const REASONS: Record<string, string> = { wrong_answer: "Wrong answer key", typo: "Typo", unclear: "Unclear", other: "Other" };

export function Support() {
  const { data: me } = useSession();
  const showTickets = can.support(me?.roles);
  const showReports = can.editContent(me?.roles);
  const [tab, setTab] = useState<"tickets" | "reports">(showTickets ? "tickets" : "reports");

  return (
    <>
      <PageHeader title="Support" description="Student tickets and reported question errors." />
      {showTickets && showReports && (
        <Tabs
          value={tab}
          onChange={setTab}
          tabs={[
            { value: "tickets", label: "Tickets" },
            { value: "reports", label: "Question reports" },
          ]}
        />
      )}
      {tab === "tickets" && showTickets ? <Tickets /> : showReports ? <Reports /> : null}
    </>
  );
}

function Tickets() {
  const [status, setStatus] = useState<(typeof TICKET_FILTERS)[number]>("Open");
  const [replying, setReplying] = useState<AdminTicket | null>(null);
  const tickets = useQuery({
    queryKey: ["admin", "tickets", status],
    queryFn: () => unwrap(api.GET("/api/v1/admin/tickets", { params: { query: { status: status || undefined } } })),
  });

  return (
    <Card
      title="Tickets"
      actions={
        <Select aria-label="Filter tickets" className="h-8 w-40" value={status} onChange={(e) => setStatus(e.target.value as typeof status)}>
          {TICKET_FILTERS.map((s) => (
            <option key={s} value={s}>
              {s ? s.replace("InProgress", "In progress") : "All"}
            </option>
          ))}
        </Select>
      }
    >
      {tickets.isPending ? (
        <SkeletonRows />
      ) : tickets.isError ? (
        <ErrorState error={tickets.error} onRetry={() => tickets.refetch()} />
      ) : tickets.data.length === 0 ? (
        <EmptyState title={status === "Open" ? "No open tickets" : "No tickets"}>New tickets from students appear here.</EmptyState>
      ) : (
        <ul className="divide-y divide-border">
          {tickets.data.map((t) => (
            <li key={t.id} className="flex flex-wrap items-start justify-between gap-3 px-4 py-3">
              <div className="min-w-0 flex-1">
                <p className="flex flex-wrap items-center gap-2 font-medium">
                  {t.subject} {t.priority === "High" && <Badge tone="danger">High priority</Badge>} <Badge>{t.category}</Badge>
                </p>
                <p className="mt-1 whitespace-pre-wrap text-sm">{truncate(t.message, 400)}</p>
                <p className="mt-1 text-xs text-fg-muted">
                  <Link href={`/admin/students/${t.userId}`} className="text-primary hover:underline">
                    {t.userName}
                  </Link>{" "}
                  · {t.userEmail ?? t.userPhone} · {formatDateTime(t.createdAt)}
                </p>
                {t.staffResponse && (
                  <p className="mt-2 rounded-lg bg-surface-muted px-3 py-2 text-sm">
                    <span className="font-medium">Reply:</span> {t.staffResponse}
                  </p>
                )}
              </div>
              <div className="flex items-center gap-2">
                <StatusBadge status={t.status} />
                <Button size="sm" variant="secondary" onClick={() => setReplying(t)}>
                  {t.staffResponse ? "Update" : "Reply"}
                </Button>
              </div>
            </li>
          ))}
        </ul>
      )}
      {replying && <ReplyModal ticket={replying} onClose={() => setReplying(null)} />}
    </Card>
  );
}

function ReplyModal({ ticket, onClose }: { ticket: AdminTicket; onClose: () => void }) {
  const [response, setResponse] = useState(ticket.staffResponse ?? "");
  const [status, setStatus] = useState<"InProgress" | "Resolved" | "Closed">("Resolved");
  const reply = useAction(() => unwrap(api.POST("/api/v1/admin/tickets/{id}/respond", { params: { path: { id: ticket.id } }, body: { response: response.trim(), status } })), {
    invalidate: [["admin", "tickets"], ["admin", "dashboard"]],
    success: "Reply sent. The student gets an in-app notification.",
    onSuccess: onClose,
  });
  return (
    <Modal
      open
      onClose={onClose}
      title={`Reply: ${ticket.subject}`}
      footer={
        <>
          <Button variant="secondary" onClick={onClose}>
            Cancel
          </Button>
          <Button disabled={!response.trim()} loading={reply.isPending} onClick={() => reply.mutate(undefined)}>
            Send reply
          </Button>
        </>
      }
    >
      <div className="space-y-4">
        <p className="whitespace-pre-wrap rounded-lg bg-surface-muted px-3 py-2 text-sm">{ticket.message}</p>
        <Field label="Your reply" htmlFor="reply-text" required>
          <Textarea id="reply-text" rows={5} maxLength={4000} value={response} onChange={(e) => setResponse(e.target.value)} />
        </Field>
        <Field label="Set status to" htmlFor="reply-status">
          <Select id="reply-status" value={status} onChange={(e) => setStatus(e.target.value as typeof status)}>
            <option value="InProgress">In progress</option>
            <option value="Resolved">Resolved</option>
            <option value="Closed">Closed</option>
          </Select>
        </Field>
      </div>
    </Modal>
  );
}

function Reports() {
  const [status, setStatus] = useState("Open");
  const reports = useQuery({
    queryKey: ["admin", "reports", status],
    queryFn: () => unwrap(api.GET("/api/v1/admin/question-reports", { params: { query: { status } } })),
  });
  const resolve = useAction(({ id, outcome }: { id: string; outcome: "Fixed" | "Rejected" }) =>
    unwrap(api.POST("/api/v1/admin/question-reports/{id}/resolve", { params: { path: { id } }, body: { status: outcome } })),
    { invalidate: [["admin", "reports"], ["admin", "dashboard"], ["admin", "questions"]], success: "Report closed" },
  );

  return (
    <Card
      title="Reported questions"
      actions={
        <Select aria-label="Filter reports" className="h-8 w-36" value={status} onChange={(e) => setStatus(e.target.value)}>
          <option value="Open">Open</option>
          <option value="Fixed">Fixed</option>
          <option value="Rejected">Rejected</option>
        </Select>
      }
    >
      {reports.isPending ? (
        <SkeletonRows />
      ) : reports.isError ? (
        <ErrorState error={reports.error} onRetry={() => reports.refetch()} />
      ) : reports.data.length === 0 ? (
        <EmptyState title="Nothing reported">When students flag a question from practice or results, it appears here.</EmptyState>
      ) : (
        <ul className="divide-y divide-border">
          {reports.data.map((r) => (
            <li key={r.id} className="flex flex-wrap items-start justify-between gap-3 px-4 py-3">
              <div className="min-w-0 flex-1">
                <Link href={`/admin/questions/${r.questionId}`} className="text-primary hover:underline">
                  {truncate(r.questionStem, 160)}
                </Link>
                <p className="mt-1 flex flex-wrap items-center gap-2 text-sm">
                  <Badge tone="warning">{REASONS[r.reason] ?? r.reason}</Badge>
                  {r.details}
                </p>
                <p className="mt-1 text-xs text-fg-muted">
                  {r.externalRef && <span className="font-mono">{r.externalRef} · </span>}
                  {formatDateTime(r.createdAt)}
                </p>
              </div>
              {r.status === "Open" ? (
                <div className="flex gap-2">
                  <Button size="sm" variant="secondary" onClick={() => resolve.mutate({ id: r.id, outcome: "Fixed" })}>
                    Mark fixed
                  </Button>
                  <Button size="sm" variant="ghost" onClick={() => resolve.mutate({ id: r.id, outcome: "Rejected" })}>
                    Reject
                  </Button>
                </div>
              ) : (
                <StatusBadge status={r.status} />
              )}
            </li>
          ))}
        </ul>
      )}
    </Card>
  );
}
