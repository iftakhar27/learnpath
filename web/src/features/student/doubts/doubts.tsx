"use client";

import Link from "next/link";
import { useParams } from "next/navigation";
import { useState } from "react";
import { useQuery, useQueryClient } from "@tanstack/react-query";
import { api, unwrap } from "@/lib/api/client";
import type { components } from "@/lib/api/schema";
import { useT, type MessageKey } from "@/lib/i18n";
import { Button } from "@/components/ui/button";
import { Modal } from "@/components/ui/dialog";
import { PageLoading } from "@/components/ui/feedback";
import { Textarea } from "@/components/ui/field";
import { Icon } from "@/components/ui/icons";
import { useToast } from "@/components/ui/toast";
import { StudentError } from "../errors";
import { routes } from "../routes";
import { Muted, PageTitle, Section, useFormat } from "../ui";

type Doubt = components["schemas"]["DoubtDto"];
/** What a new doubt is about (one is enough; the API works out the topic and course). */
export type DoubtLink = { topicId?: string; resourceId?: string; questionId?: string };

export const doubtsKey = ["student", "doubts"] as const;
const doubtKey = (id: string) => ["student", "doubts", id] as const;
const MIN_LENGTH = 5;
const MAX_LENGTH = 2000;

const STATUS: Record<string, { label: MessageKey; tone: string }> = {
  Open: { label: "doubtStatusOpen", tone: "bg-warning-soft text-warning" },
  Answered: { label: "doubtStatusAnswered", tone: "bg-success-soft text-success" },
  Resolved: { label: "doubtStatusResolved", tone: "bg-surface-muted text-fg-muted" },
};

function StatusBadge({ status }: { status: string }) {
  const t = useT();
  const s = STATUS[status] ?? STATUS.Open;
  return <span className={`inline-flex shrink-0 items-center rounded-full px-2 py-0.5 text-xs font-medium ${s.tone}`}>{t(s.label)}</span>;
}

/** Text box + send. Used on the doubts page and in the "Ask a doubt" dialog. */
function AskForm({ link, about, onSent, autoFocus }: { link: DoubtLink; about?: string | null; onSent: (d: Doubt) => void; autoFocus?: boolean }) {
  const t = useT();
  const [text, setText] = useState("");
  const [touched, setTouched] = useState(false);
  const [sending, setSending] = useState(false);
  const [error, setError] = useState<unknown>(null);
  const tooShort = text.trim().length < MIN_LENGTH;

  const send = async () => {
    setTouched(true);
    if (tooShort) return;
    setSending(true);
    setError(null);
    try {
      const d = await unwrap(api.POST("/api/v1/doubts", { body: { text: text.trim(), topicId: link.topicId ?? null, resourceId: link.resourceId ?? null, questionId: link.questionId ?? null } }));
      setText("");
      setTouched(false);
      onSent(d);
    } catch (e) {
      setError(e);
    } finally {
      setSending(false);
    }
  };

  return (
    <form
      className="space-y-3"
      noValidate
      onSubmit={(e) => {
        e.preventDefault();
        void send();
      }}
    >
      <StudentError error={error} />
      {about && <p className="text-sm text-fg-muted">{t("doubtAbout", { title: about })}</p>}
      <label className="block space-y-1">
        <span className="block text-sm font-medium">{t("yourDoubt")}</span>
        <Textarea
          rows={5}
          maxLength={MAX_LENGTH}
          value={text}
          autoFocus={autoFocus}
          placeholder={t("doubtHint")}
          aria-invalid={touched && tooShort}
          onChange={(e) => setText(e.target.value)}
        />
      </label>
      {touched && tooShort && (
        <p className="text-xs text-danger" role="alert">
          {t("doubtTooShort")}
        </p>
      )}
      <Button type="submit" loading={sending}>
        <Icon name="chat" size={16} />
        {t("send")}
      </Button>
    </form>
  );
}

/** "Ask a doubt" button that opens a dialog, for topic, lesson and question screens. */
export function AskDoubtButton({ link, about, className = "" }: { link: DoubtLink; about?: string | null; className?: string }) {
  const t = useT();
  const toast = useToast();
  const qc = useQueryClient();
  const [open, setOpen] = useState(false);
  return (
    <>
      <button
        type="button"
        onClick={() => setOpen(true)}
        className={`inline-flex items-center gap-1.5 rounded-lg border border-border px-3 py-1.5 text-sm font-medium hover:bg-surface-muted ${className}`}
      >
        <Icon name="chat" size={16} />
        {t("askDoubt")}
      </button>
      <Modal open={open} onClose={() => setOpen(false)} title={t("askDoubt")} closeLabel={t("cancel")}>
        {open && (
          <AskForm
            link={link}
            about={about}
            autoFocus
            onSent={() => {
              setOpen(false);
              toast(t("doubtSent"));
              void qc.invalidateQueries({ queryKey: doubtsKey });
            }}
          />
        )}
      </Modal>
    </>
  );
}

export function DoubtsPage() {
  const t = useT();
  const f = useFormat();
  const toast = useToast();
  const qc = useQueryClient();
  const list = useQuery({ queryKey: doubtsKey, queryFn: () => unwrap(api.GET("/api/v1/doubts", { params: { query: { pageSize: 50 } } })) });

  return (
    <div className="mx-auto max-w-2xl space-y-6">
      <PageTitle subtitle={t("doubtsIntro")}>{t("doubtsTitle")}</PageTitle>
      <Section title={t("askDoubt")}>
        <AskForm
          link={{}}
          onSent={() => {
            toast(t("doubtSent"));
            void qc.invalidateQueries({ queryKey: doubtsKey });
          }}
        />
      </Section>
      {list.isPending ? (
        <PageLoading />
      ) : list.isError ? (
        <StudentError error={list.error} onRetry={() => list.refetch()} />
      ) : list.data.items.length === 0 ? (
        <Muted>{t("doubtsEmpty")}</Muted>
      ) : (
        <ul className="space-y-3">
          {list.data.items.map((d) => (
            <li key={d.id}>
              <Link href={routes.doubt(d.id)} className="block rounded-2xl border border-border bg-surface p-4 hover:border-primary">
                <div className="flex items-start justify-between gap-3">
                  <p className="line-clamp-2 min-w-0 font-medium">{d.text}</p>
                  <StatusBadge status={d.status} />
                </div>
                <p className="mt-2 text-sm text-fg-muted">
                  {d.context.topic ? t("doubtAbout", { title: d.context.topic }) : t("doubtGeneral")} ·{" "}
                  {t("doubtReplies", { count: d.replies })} · {f.dateTime(d.updatedAt)}
                </p>
              </Link>
            </li>
          ))}
        </ul>
      )}
    </div>
  );
}

export function DoubtPage() {
  const t = useT();
  const f = useFormat();
  const qc = useQueryClient();
  const { doubtId } = useParams<{ doubtId: string }>();
  const q = useQuery({ queryKey: doubtKey(doubtId), queryFn: () => unwrap(api.GET("/api/v1/doubts/{id}", { params: { path: { id: doubtId } } })) });
  const [reply, setReply] = useState("");
  const [busy, setBusy] = useState<"reply" | "resolve" | null>(null);
  const [error, setError] = useState<unknown>(null);

  const act = async (kind: "reply" | "resolve") => {
    setBusy(kind);
    setError(null);
    try {
      const d =
        kind === "reply"
          ? await unwrap(api.POST("/api/v1/doubts/{id}/messages", { params: { path: { id: doubtId } }, body: { text: reply.trim() } }))
          : await unwrap(api.POST("/api/v1/doubts/{id}/resolve", { params: { path: { id: doubtId } } }));
      qc.setQueryData(doubtKey(doubtId), d);
      if (kind === "reply") setReply("");
      void qc.invalidateQueries({ queryKey: doubtsKey, exact: true });
    } catch (e) {
      setError(e);
    } finally {
      setBusy(null);
    }
  };

  if (q.isPending) return <PageLoading />;
  if (q.isError) return <StudentError error={q.error} onRetry={() => q.refetch()} />;
  const d = q.data;
  const c = d.context;

  return (
    <div className="mx-auto max-w-2xl space-y-5">
      <Link href={routes.doubts} className="inline-flex items-center gap-1 text-sm font-medium text-primary hover:underline">
        <Icon name="chevronLeft" size={16} />
        {t("doubtsTitle")}
      </Link>

      <section className="rounded-2xl border border-border bg-surface p-5">
        <div className="flex items-start justify-between gap-3">
          <p className="text-sm text-fg-muted">
            {c.topicId && c.topic ? (
              <Link href={routes.topic(c.topicId)} className="font-medium text-primary hover:underline">
                {c.topic}
              </Link>
            ) : (
              t("doubtGeneral")
            )}
            {c.course && <> · {c.course}</>}
          </p>
          <StatusBadge status={d.status} />
        </div>
        {(c.question || c.resource) && <p className="mt-2 line-clamp-3 rounded-lg bg-surface-muted px-3 py-2 text-sm">{c.question ?? c.resource}</p>}
        <p className="mt-3 whitespace-pre-line">{d.text}</p>
        <p className="mt-2 text-xs text-fg-muted">{f.dateTime(d.createdAt)}</p>
      </section>

      <ol className="space-y-3" aria-label={t("doubtReplies", { count: d.messages.filter((m) => m.fromStaff).length })}>
        {d.messages.map((m) => (
          <li key={m.id} className={`flex ${m.fromStaff ? "justify-start" : "justify-end"}`}>
            <div className={`max-w-[85%] rounded-2xl px-4 py-3 ${m.fromStaff ? "border border-border bg-surface" : "bg-primary-soft"}`}>
              <p className="text-xs font-medium text-fg-muted">
                {m.fromStaff ? `${t("doubtTeacher")} · ${m.author}` : t("doubtYou")} · {f.dateTime(m.createdAt)}
              </p>
              <p className="mt-1 whitespace-pre-line">{m.text}</p>
            </div>
          </li>
        ))}
      </ol>

      <section className="space-y-3 rounded-2xl border border-border bg-surface p-5">
        <StudentError error={error} />
        {d.status === "Resolved" && <Muted>{t("doubtResolvedHint")}</Muted>}
        <label className="block space-y-1">
          <span className="block text-sm font-medium">{t("doubtFollowUp")}</span>
          <Textarea rows={3} maxLength={MAX_LENGTH} value={reply} onChange={(e) => setReply(e.target.value)} />
        </label>
        <div className="flex flex-wrap gap-2">
          <Button onClick={() => act("reply")} loading={busy === "reply"} disabled={!reply.trim() || busy !== null}>
            {t("send")}
          </Button>
          {d.status !== "Resolved" && (
            <Button variant="secondary" onClick={() => act("resolve")} loading={busy === "resolve"} disabled={busy !== null}>
              <Icon name="check" size={16} />
              {t("doubtMarkResolved")}
            </Button>
          )}
        </div>
      </section>
    </div>
  );
}
