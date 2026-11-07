"use client";

import Link from "next/link";
import { useParams, useRouter } from "next/navigation";
import { useCallback, useEffect, useRef, useState, useSyncExternalStore } from "react";
import { useQuery, useQueryClient } from "@tanstack/react-query";
import { ApiError, api, unwrap } from "@/lib/api/client";
import type { components } from "@/lib/api/schema";
import { useT, type MessageKey } from "@/lib/i18n";
import { Button } from "@/components/ui/button";
import { Modal } from "@/components/ui/dialog";
import { PageLoading } from "@/components/ui/feedback";
import { Icon } from "@/components/ui/icons";
import { StudentError } from "../errors";
import { QuestionView } from "../question";
import { routes } from "../routes";
import { useFormat } from "../ui";
import { ExamSession, paletteStatus, type PaletteStatus } from "./exam-session";

const STATUS_STYLE: Record<PaletteStatus, string> = {
  answered: "border-success bg-success text-white",
  notAnswered: "border-danger bg-danger-soft text-danger",
  marked: "border-primary bg-primary-soft text-primary",
  answeredMarked: "border-primary bg-primary text-primary-fg",
  notVisited: "border-border bg-surface text-fg-muted",
};
const STATUS_LABEL: Record<PaletteStatus, MessageKey> = {
  answered: "paletteAnswered",
  notAnswered: "paletteNotAnswered",
  marked: "paletteMarked",
  answeredMarked: "paletteMarked",
  notVisited: "paletteNotVisited",
};

function useSession(session: ExamSession) {
  useSyncExternalStore(session.subscribe, session.getVersion, session.getVersion);
  return session;
}

function useTick() {
  const [, setTick] = useState(0);
  useEffect(() => {
    const id = setInterval(() => setTick((n) => n + 1), 1000);
    return () => clearInterval(id);
  }, []);
}

function Palette({ session, onPick }: { session: ExamSession; onPick?: () => void }) {
  const t = useT();
  useSession(session);
  const total = session.questions.length;
  return (
    <div className="space-y-4">
      <ol className="grid grid-cols-6 gap-2 sm:grid-cols-8 lg:grid-cols-5">
        {session.questions.map((q, i) => {
          const status = paletteStatus(q);
          return (
            <li key={session.attempt.questions[i].testQuestionId}>
              <button
                type="button"
                onClick={() => {
                  session.goTo(i);
                  onPick?.();
                }}
                aria-current={i === session.current ? "step" : undefined}
                aria-label={`${t("questionOf", { current: i + 1, total })}, ${t(STATUS_LABEL[status])}`}
                className={`relative flex aspect-square w-full items-center justify-center rounded-lg border text-sm font-semibold tabular-nums ${STATUS_STYLE[status]} ${i === session.current ? "ring-2 ring-[var(--focus)] ring-offset-2 ring-offset-[var(--surface)]" : ""}`}
              >
                {i + 1}
                {status === "answeredMarked" && <span className="absolute right-1 top-1 size-1.5 rounded-full bg-success" aria-hidden />}
              </button>
            </li>
          );
        })}
      </ol>
      <ul className="grid grid-cols-2 gap-2 text-xs">
        {(["answered", "notAnswered", "marked", "notVisited"] as const).map((s) => (
          <li key={s} className="flex items-center gap-2">
            <span className={`size-4 rounded border ${STATUS_STYLE[s]}`} aria-hidden />
            <span>
              {t(STATUS_LABEL[s])} ({s === "marked" ? session.count("marked") + session.count("answeredMarked") : session.count(s)})
            </span>
          </li>
        ))}
      </ul>
    </div>
  );
}

function Timer({ session }: { session: ExamSession }) {
  const t = useT();
  const f = useFormat();
  useTick();
  const left = session.remainingSeconds();
  const tone = left <= 60 ? "bg-danger text-white" : left <= 300 ? "bg-warning-soft text-warning" : "bg-surface-muted text-fg";
  return (
    <span role="timer" aria-label={t("timeLeft")} className={`inline-flex items-center gap-1.5 rounded-lg px-3 py-1.5 font-semibold tabular-nums ${tone}`}>
      <Icon name="clock" size={16} />
      {f.duration(left)}
    </span>
  );
}

function SyncStatus({ session }: { session: ExamSession }) {
  const t = useT();
  useSession(session);
  if (session.sync === "offline")
    return (
      <span role="status" className="text-sm font-medium text-warning">
        {t("offlineWillRetry")}
      </span>
    );
  return (
    <span role="status" className="text-sm text-fg-muted">
      {session.sync === "saving" ? t("saving") : session.savedOnce ? t("saved") : ""}
    </span>
  );
}

function ExamPlayer({ session }: { session: ExamSession }) {
  const t = useT();
  const router = useRouter();
  const qc = useQueryClient();
  useSession(session);
  useTick();
  const [confirm, setConfirm] = useState(false);
  const [leave, setLeave] = useState(false);
  const [palette, setPalette] = useState(false);
  const [submitting, setSubmitting] = useState<"manual" | "auto" | null>(null);
  const [error, setError] = useState<unknown>(null);
  const total = session.questions.length;
  const q = session.question;
  const state = session.state;

  const submit = useCallback(
    async (mode: "manual" | "auto") => {
      setSubmitting(mode);
      setError(null);
      await session.prepareSubmit();
      try {
        await unwrap(api.POST("/api/v1/attempts/{attemptId}/submit", { params: { path: { attemptId: session.attempt.attemptId } } }));
      } catch (e) {
        // Already submitted or auto-submitted by the server: the result exists, show it.
        if (!(e instanceof ApiError && (e.status === 409 || e.status === 400))) {
          setSubmitting(null);
          setError(e);
          return;
        }
      }
      await qc.invalidateQueries({ queryKey: ["student"] });
      router.replace(routes.result(session.attempt.attemptId));
    },
    [session, qc, router],
  );

  // The server enforces the time limit; the browser submits as soon as it runs out.
  const timeUp = session.remainingSeconds() <= 0;
  const autoStarted = useRef(false);
  useEffect(() => {
    if (timeUp && !autoStarted.current) {
      autoStarted.current = true;
      void submit("auto");
    }
  }, [timeUp, submit]);

  // Warn before closing the tab while answers are still being saved.
  useEffect(() => {
    const onUnload = (e: BeforeUnloadEvent) => {
      if (session.hasUnsaved) e.preventDefault();
    };
    const onHide = () => document.visibilityState === "hidden" && void session.flush();
    window.addEventListener("beforeunload", onUnload);
    document.addEventListener("visibilitychange", onHide);
    return () => {
      window.removeEventListener("beforeunload", onUnload);
      document.removeEventListener("visibilitychange", onHide);
    };
  }, [session]);

  if (submitting === "auto")
    return (
      <div role="alert" className="flex min-h-dvh items-center justify-center p-6 text-center text-lg font-medium">
        {t("timeUp")}
      </div>
    );

  return (
    <div className="flex min-h-dvh flex-col">
      <header className="sticky top-0 z-20 border-b border-border bg-surface pt-[env(safe-area-inset-top)]">
        <div className="mx-auto flex h-14 max-w-6xl items-center gap-3 px-4">
          <button type="button" onClick={() => setLeave(true)} className="rounded-lg p-2 text-fg-muted hover:bg-surface-muted" aria-label={t("leave")}>
            <Icon name="x" />
          </button>
          <h1 className="min-w-0 flex-1 truncate font-semibold">{session.attempt.testTitle}</h1>
          <div className="hidden sm:block">
            <SyncStatus session={session} />
          </div>
          <Timer session={session} />
          <Button onClick={() => setConfirm(true)} size="sm">
            {t("submit")}
          </Button>
        </div>
      </header>

      <div className="mx-auto grid w-full max-w-6xl flex-1 gap-6 px-4 py-6 lg:grid-cols-[1fr_300px]">
        <main className="space-y-4">
          <div className="flex flex-wrap items-center justify-between gap-2 text-sm text-fg-muted">
            <span className="font-medium text-fg">{t("questionOf", { current: session.current + 1, total })}</span>
            <span>
              {[q.section, q.subject].filter(Boolean).join(" · ")} · +{q.marks}
              {q.negativeMarks ? ` / −${q.negativeMarks}` : ""}
            </span>
          </div>
          <section className="rounded-2xl border border-border bg-surface p-5 sm:p-6">
            <QuestionView
              key={q.testQuestionId}
              type={q.type}
              stem={q.stem}
              imageUrl={q.imageUrl}
              options={q.options}
              answer={state.response}
              onChange={(a, typing) => session.setResponse(a, typing)}
              label={t("questionOf", { current: session.current + 1, total })}
            />
          </section>
          <div className="sm:hidden">
            <SyncStatus session={session} />
          </div>
          <StudentError error={error} />
          <div className="flex flex-wrap items-center gap-2">
            <Button variant="secondary" onClick={() => session.goTo(session.current - 1)} disabled={session.current === 0}>
              <Icon name="chevronLeft" size={16} />
              {t("previous")}
            </Button>
            <Button variant="ghost" onClick={() => session.setResponse(null)}>
              {t("clearResponse")}
            </Button>
            <Button variant="secondary" onClick={() => session.toggleMark()} aria-pressed={state.marked}>
              <Icon name="flag" size={16} />
              {state.marked ? t("unmarkReview") : t("markForReview")}
            </Button>
            <Button className="ml-auto" onClick={() => (session.current < total - 1 ? session.goTo(session.current + 1) : setConfirm(true))}>
              {t("saveAndNext")}
              <Icon name="chevronRight" size={16} />
            </Button>
          </div>
          <Button variant="secondary" className="w-full lg:hidden" onClick={() => setPalette(true)}>
            {t("questionPalette")}
          </Button>
        </main>
        <aside className="hidden lg:block" aria-label={t("questionPalette")}>
          <div className="sticky top-20 rounded-2xl border border-border bg-surface p-4">
            <h2 className="mb-3 font-semibold">{t("questionPalette")}</h2>
            <Palette session={session} />
          </div>
        </aside>
      </div>

      <Modal open={palette} onClose={() => setPalette(false)} title={t("questionPalette")} closeLabel={t("cancel")}>
        <Palette session={session} onPick={() => setPalette(false)} />
      </Modal>
      <Modal
        open={confirm}
        onClose={() => setConfirm(false)}
        title={t("submitConfirmTitle")}
        closeLabel={t("keepWorking")}
        footer={
          <>
            <Button variant="secondary" onClick={() => setConfirm(false)} disabled={!!submitting}>
              {t("keepWorking")}
            </Button>
            <Button onClick={() => void submit("manual")} loading={submitting === "manual"}>
              {t("submit")}
            </Button>
          </>
        }
      >
        <p className="whitespace-pre-line">{t("submitConfirmBody", { answered: session.answeredCount, marked: session.markedCount, unanswered: total - session.answeredCount })}</p>
      </Modal>
      <Modal
        open={leave}
        onClose={() => setLeave(false)}
        title={t("leaveTestTitle")}
        closeLabel={t("stay")}
        footer={
          <>
            <Button variant="secondary" onClick={() => setLeave(false)}>
              {t("stay")}
            </Button>
            <Link href={routes.tests} className="inline-flex h-10 items-center rounded-lg bg-primary px-4 text-sm font-medium text-primary-fg hover:bg-primary-hover" onClick={() => void session.flush()}>
              {t("leave")}
            </Link>
          </>
        }
      >
        <p>{t("leaveTestBody")}</p>
      </Modal>
    </div>
  );
}

export function ExamPage() {
  const router = useRouter();
  const { attemptId } = useParams<{ attemptId: string }>();
  const q = useQuery({
    queryKey: ["exam", attemptId],
    queryFn: () => unwrap(api.GET("/api/v1/attempts/{attemptId}", { params: { path: { attemptId } } })),
    staleTime: Infinity,
    gcTime: 0,
    meta: { keepOnLanguageChange: true },
  });
  const data = q.data;
  const closed = data && data.status !== "InProgress";
  useEffect(() => {
    if (closed) router.replace(routes.result(attemptId));
  }, [closed, router, attemptId]);

  if (q.isError)
    return (
      <div className="mx-auto max-w-md p-6">
        <StudentError error={q.error} onRetry={() => q.refetch()} />
      </div>
    );
  if (!data || closed) return <PageLoading />;
  return <ExamLoaded attempt={data} />;
}

/**
 * Holds one session per visit, created from the first load: a later refetch must never reset unsaved answers.
 * Pending saves keep retrying even after leaving the page, so nothing answered is lost.
 */
function ExamLoaded({ attempt }: { attempt: components["schemas"]["AttemptDto"] }) {
  const [session] = useState(
    () => new ExamSession(attempt, (body) => unwrap(api.PUT("/api/v1/attempts/{attemptId}/answers", { params: { path: { attemptId: attempt.attemptId } }, body }))),
  );
  return <ExamPlayer session={session} />;
}
