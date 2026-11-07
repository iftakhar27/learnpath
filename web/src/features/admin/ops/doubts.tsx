"use client";

import Link from "next/link";
import { useParams } from "next/navigation";
import { useState } from "react";
import { useQuery, useQueryClient } from "@tanstack/react-query";
import { api, unwrap } from "@/lib/api/client";
import type { components } from "@/lib/api/schema";
import { formatDateTime, truncate } from "@/lib/format";
import { Button } from "@/components/ui/button";
import { Badge, EmptyState, ErrorState, SkeletonRows } from "@/components/ui/feedback";
import { Field, Select, Textarea } from "@/components/ui/field";
import { Card, PageHeader, Pagination } from "@/components/ui/layout";
import { SearchInput } from "@/components/ui/search-input";
import { useAction } from "../shared";

type Status = components["schemas"]["DoubtStatus"];
type Context = components["schemas"]["DoubtContextDto"];

const FILTERS: { value: Status | ""; label: string }[] = [
  { value: "Open", label: "Waiting for a teacher" },
  { value: "Answered", label: "Answered" },
  { value: "Resolved", label: "Resolved" },
  { value: "", label: "All" },
];
const TONE: Record<Status, "warning" | "success" | "neutral"> = { Open: "warning", Answered: "success", Resolved: "neutral" };
const LABEL: Record<Status, string> = { Open: "Waiting", Answered: "Answered", Resolved: "Resolved" };
const PAGE_SIZE = 20;

function ContextLine({ c }: { c: Context }) {
  if (!c.topic && !c.course) return <span>General doubt</span>;
  return (
    <span>
      {c.topic}
      {c.course && <> · {c.course}</>}
    </span>
  );
}

/** Teachers' inbox: students' doubts, oldest waiting first. */
export function Doubts() {
  const [status, setStatus] = useState<Status | "">("Open");
  const [q, setQ] = useState("");
  const [page, setPage] = useState(1);
  const list = useQuery({
    queryKey: ["admin", "doubts", status, q, page],
    queryFn: () => unwrap(api.GET("/api/v1/admin/doubts", { params: { query: { status: status || undefined, q: q || undefined, page, pageSize: PAGE_SIZE } } })),
  });

  return (
    <>
      <PageHeader title="Doubts" description="Questions students asked about topics, lessons and practice questions. Students get a notification when you reply." />
      <Card
        title="Doubts"
        actions={
          <Select aria-label="Filter doubts" className="h-8 w-48" value={status} onChange={(e) => (setStatus(e.target.value as Status | ""), setPage(1))}>
            {FILTERS.map((f) => (
              <option key={f.value} value={f.value}>
                {f.label}
              </option>
            ))}
          </Select>
        }
      >
        <div className="max-w-sm px-4 pt-3">
          <SearchInput key={q} id="doubt-search" label="Search" placeholder="Words from the doubt or the student's name" value={q} onSearch={(v) => (setQ(v), setPage(1))} />
        </div>
        {list.isPending ? (
          <SkeletonRows />
        ) : list.isError ? (
          <ErrorState error={list.error} onRetry={() => list.refetch()} />
        ) : list.data.items.length === 0 ? (
          <EmptyState title={status === "Open" ? "No doubts waiting" : "No doubts"}>New doubts from students appear here.</EmptyState>
        ) : (
          <>
            <ul className="divide-y divide-border">
              {list.data.items.map((d) => (
                <li key={d.id}>
                  <Link href={`/admin/doubts/${d.id}`} className="flex flex-wrap items-start justify-between gap-3 px-4 py-3 hover:bg-surface-muted">
                    <div className="min-w-0 flex-1">
                      <p className="font-medium">{truncate(d.text, 200)}</p>
                      <p className="mt-1 text-xs text-fg-muted">
                        {d.studentName} · <ContextLine c={d.context} /> · {d.messages} {d.messages === 1 ? "reply" : "replies"} · {formatDateTime(d.updatedAt)}
                      </p>
                    </div>
                    <Badge tone={TONE[d.status]}>{LABEL[d.status]}</Badge>
                  </Link>
                </li>
              ))}
            </ul>
            <Pagination page={page} pageSize={PAGE_SIZE} total={list.data.total} onPage={setPage} />
          </>
        )}
      </Card>
    </>
  );
}

export function DoubtThread() {
  const { doubtId } = useParams<{ doubtId: string }>();
  const qc = useQueryClient();
  const key = ["admin", "doubts", "detail", doubtId];
  const q = useQuery({ queryKey: key, queryFn: () => unwrap(api.GET("/api/v1/admin/doubts/{id}", { params: { path: { id: doubtId } } })) });
  const [text, setText] = useState("");
  const [resolve, setResolve] = useState(false);
  const after = (d: components["schemas"]["AdminDoubtDto"]) => qc.setQueryData(key, d);
  const reply = useAction(() => unwrap(api.POST("/api/v1/admin/doubts/{id}/reply", { params: { path: { id: doubtId } }, body: { text: text.trim(), resolve } })), {
    invalidate: [["admin", "doubts"], ["admin", "dashboard"]],
    success: "Reply sent. The student gets an in-app notification.",
    onSuccess: (d) => {
      after(d);
      setText("");
      setResolve(false);
    },
  });
  const close = useAction(() => unwrap(api.POST("/api/v1/admin/doubts/{id}/resolve", { params: { path: { id: doubtId } } })), {
    invalidate: [["admin", "doubts"], ["admin", "dashboard"]],
    success: "Marked as resolved",
    onSuccess: after,
  });

  if (q.isPending) return <SkeletonRows />;
  if (q.isError) return <ErrorState error={q.error} onRetry={() => q.refetch()} />;
  const d = q.data;
  const c = d.context;

  return (
    <>
      <PageHeader
        title="Doubt"
        back={
          <Link href="/admin/doubts" className="text-sm text-primary hover:underline">
            ← All doubts
          </Link>
        }
        actions={<Badge tone={TONE[d.status]}>{LABEL[d.status]}</Badge>}
      />
      <div className="grid gap-6 lg:grid-cols-3">
        <div className="space-y-4 lg:col-span-2">
          <Card title="Question">
            <div className="space-y-3 p-4">
              <p className="whitespace-pre-wrap">{d.text}</p>
              <p className="text-xs text-fg-muted">
                {d.studentName} · {formatDateTime(d.createdAt)}
              </p>
            </div>
          </Card>
          {d.messages.length > 0 && (
            <ol className="space-y-3" aria-label="Replies">
              {d.messages.map((m) => (
                <li key={m.id} className={`rounded-xl border p-4 ${m.fromStaff ? "border-primary/30 bg-primary-soft" : "border-border bg-surface"}`}>
                  <p className="text-xs font-medium text-fg-muted">
                    {m.fromStaff ? `${m.author} (staff)` : `${m.author} (student)`} · {formatDateTime(m.createdAt)}
                  </p>
                  <p className="mt-1 whitespace-pre-wrap text-sm">{m.text}</p>
                </li>
              ))}
            </ol>
          )}
          <Card title="Reply">
            <div className="space-y-3 p-4">
              <Field label="Your answer" htmlFor="doubt-reply" required>
                <Textarea id="doubt-reply" rows={6} maxLength={2000} value={text} onChange={(e) => setText(e.target.value)} />
              </Field>
              <label className="flex items-center gap-2 text-sm">
                <input type="checkbox" className="size-4" checked={resolve} onChange={(e) => setResolve(e.target.checked)} />
                Also mark as resolved (the student can still follow up)
              </label>
              <div className="flex flex-wrap gap-2">
                <Button disabled={!text.trim()} loading={reply.isPending} onClick={() => reply.mutate(undefined)}>
                  Send reply
                </Button>
                {d.status !== "Resolved" && (
                  <Button variant="ghost" loading={close.isPending} onClick={() => close.mutate(undefined)}>
                    Resolve without replying
                  </Button>
                )}
              </div>
            </div>
          </Card>
        </div>
        <Card title="Context" className="self-start">
          <dl className="space-y-3 p-4 text-sm">
            <div>
              <dt className="text-fg-muted">Student</dt>
              <dd>
                <Link href={`/admin/students/${d.userId}`} className="text-primary hover:underline">
                  {d.studentName}
                </Link>
                <span className="block text-fg-muted">{d.studentEmail ?? d.studentPhone}</span>
              </dd>
            </div>
            <div>
              <dt className="text-fg-muted">Course</dt>
              <dd>
                {c.courseId ? (
                  <Link href={`/admin/catalog/${c.courseId}`} className="text-primary hover:underline">
                    {c.course}
                  </Link>
                ) : (
                  "General doubt"
                )}
              </dd>
            </div>
            {c.topic && (
              <div>
                <dt className="text-fg-muted">Topic</dt>
                <dd>{c.topic}</dd>
              </div>
            )}
            {c.resource && (
              <div>
                <dt className="text-fg-muted">Lesson</dt>
                <dd>{c.resource}</dd>
              </div>
            )}
            {c.questionId && (
              <div>
                <dt className="text-fg-muted">Question</dt>
                <dd>
                  <Link href={`/admin/questions/${c.questionId}`} className="text-primary hover:underline">
                    {truncate(c.question ?? "Open question", 200)}
                  </Link>
                </dd>
              </div>
            )}
          </dl>
        </Card>
      </div>
    </>
  );
}
