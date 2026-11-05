"use client";

import Link from "next/link";
import { useParams } from "next/navigation";
import { useEffect, useRef, useState } from "react";
import { useMutation } from "@tanstack/react-query";
import { api, unwrap } from "@/lib/api/client";
import type { components } from "@/lib/api/schema";
import { useT, type MessageKey } from "@/lib/i18n";
import { Button, buttonClass } from "@/components/ui/button";
import { PageLoading } from "@/components/ui/feedback";
import { Icon, type IconName } from "@/components/ui/icons";
import { StudentError } from "../errors";
import { QuestionView, isAnswered, isOptionType, type Answer } from "../question";
import { routes } from "../routes";
import { PageTitle, ProgressBar } from "../ui";
import { QuestionTools } from "./report";

type CreateRequest = components["schemas"]["CreatePracticeRequest"];
type Feedback = components["schemas"]["PracticeFeedbackDto"];
type Mode = "incorrect" | "bookmarked" | "previous_year";

const MODES: { mode: Mode; title: MessageKey; hint: MessageKey; icon: IconName }[] = [
  { mode: "incorrect", title: "practiceMistakes", hint: "practiceMistakesHint", icon: "target" },
  { mode: "bookmarked", title: "practiceBookmarked", hint: "practiceBookmarkedHint", icon: "bookmark" },
  { mode: "previous_year", title: "practicePyq", hint: "practicePyqHint", icon: "clock" },
];

export function PracticeHome() {
  const t = useT();
  return (
    <>
      <PageTitle>{t("practiceTitle")}</PageTitle>
      <ul className="grid gap-4 sm:grid-cols-2">
        {MODES.map((m) => (
          <li key={m.mode}>
            <Link href={routes.practiceMode(m.mode)} className="flex h-full items-start gap-4 rounded-2xl border border-border bg-surface p-5 hover:border-primary">
              <span className="rounded-xl bg-primary-soft p-2.5 text-primary">
                <Icon name={m.icon} />
              </span>
              <span>
                <span className="block font-semibold">{t(m.title)}</span>
                <span className="mt-1 block text-sm text-fg-muted">{t(m.hint)}</span>
              </span>
            </Link>
          </li>
        ))}
        <li>
          <Link href={routes.learn} className="flex h-full items-start gap-4 rounded-2xl border border-dashed border-border p-5 hover:border-primary">
            <span className="rounded-xl bg-surface-muted p-2.5 text-fg-muted">
              <Icon name="book" />
            </span>
            <span>
              <span className="block font-semibold">{t("practiceByTopic")}</span>
              <span className="mt-1 block text-sm text-fg-muted">{t("practiceByTopicHint")}</span>
            </span>
          </Link>
        </li>
      </ul>
    </>
  );
}

export function PracticeTopicPage() {
  const { topicId } = useParams<{ topicId: string }>();
  return <PracticeSession request={{ mode: "topic", topicId, count: 10 }} backHref={routes.topic(topicId)} />;
}

export function PracticeModePage() {
  const { mode } = useParams<{ mode: string }>();
  const valid = MODES.some((m) => m.mode === mode);
  return valid ? <PracticeSession request={{ mode, count: 10 }} backHref={routes.practice} /> : <PracticeHome />;
}

/** One practice run: answer, check, read the explanation, move on; a summary at the end. */
export function PracticeSession({ request, backHref }: { request: CreateRequest; backHref: string }) {
  const t = useT();
  // Starting a session is an action (it picks questions), so it runs once per visit rather than as a cached query.
  const start = useMutation({ mutationFn: () => unwrap(api.POST("/api/v1/practice/sessions", { body: request })) });
  const started = useRef(false);
  useEffect(() => {
    if (started.current) return;
    started.current = true;
    start.mutate();
  }, [start]);

  const [index, setIndex] = useState(0);
  const [answer, setAnswer] = useState<Answer | null>(null);
  const [feedback, setFeedback] = useState<Feedback | null>(null);
  const [showHint, setShowHint] = useState(false);
  const shownAt = useRef(0);
  const check = useMutation({
    mutationFn: (body: components["schemas"]["PracticeAnswerRequest"]) =>
      unwrap(api.POST("/api/v1/practice/sessions/{sessionId}/answers", { params: { path: { sessionId: start.data!.sessionId } }, body })),
    onSuccess: setFeedback,
  });
  const finish = useMutation({
    mutationFn: () => unwrap(api.POST("/api/v1/practice/sessions/{sessionId}/complete", { params: { path: { sessionId: start.data!.sessionId } } })),
  });

  useEffect(() => {
    shownAt.current = Date.now();
  }, [index, start.data]);

  if (start.isIdle || start.isPending) return <PageLoading />;
  if (start.isError) return <StudentError error={start.error} onRetry={() => start.mutate()} />;
  const session = start.data;

  if (session.questions.length === 0)
    return (
      <div className="mx-auto max-w-xl space-y-4 text-center">
        <p className="text-fg-muted">{t("noQuestionsForSelection")}</p>
        <Link href={backHref} className={buttonClass("secondary")}>
          {t("back")}
        </Link>
      </div>
    );

  if (finish.data) {
    const s = finish.data;
    return (
      <div className="mx-auto max-w-xl space-y-6">
        <h1 className="text-2xl font-semibold">{t("practiceSummary")}</h1>
        <dl className="grid grid-cols-2 gap-3 sm:grid-cols-4">
          {(
            [
              ["accuracy", `${Math.round(s.accuracy)}%`],
              ["correct", s.correct],
              ["incorrect", s.incorrect],
              ["skipped", s.total - s.answered],
            ] as const
          ).map(([k, v]) => (
            <div key={k} className="rounded-xl border border-border bg-surface p-4">
              <dt className="text-sm text-fg-muted">{t(k)}</dt>
              <dd className="mt-1 text-2xl font-semibold tabular-nums">{v}</dd>
            </div>
          ))}
        </dl>
        <Link href={backHref} className={buttonClass("primary")}>
          {t("done")}
        </Link>
      </div>
    );
  }

  const q = session.questions[index];
  const last = index === session.questions.length - 1;
  const outcomeKey: Record<string, MessageKey> = { Correct: "correct", Incorrect: "incorrect", PartiallyCorrect: "partiallyCorrect", Skipped: "skipped" };

  const next = () => {
    setIndex((i) => i + 1);
    setAnswer(null);
    setFeedback(null);
    setShowHint(false);
    check.reset();
  };

  return (
    <div className="mx-auto max-w-3xl space-y-5">
      <div className="flex items-center justify-between gap-3">
        <Link href={backHref} className="inline-flex items-center gap-1 text-sm font-medium text-primary hover:underline">
          <Icon name="chevronLeft" size={16} />
          {t("back")}
        </Link>
        <span className="text-sm text-fg-muted">{t("questionOf", { current: index + 1, total: session.questions.length })}</span>
      </div>
      <ProgressBar value={((index + (feedback ? 1 : 0)) / session.questions.length) * 100} label={t("questionOf", { current: index + 1, total: session.questions.length })} />

      <section className="rounded-2xl border border-border bg-surface p-5 sm:p-6">
        <QuestionView
          key={q.questionId}
          type={q.type}
          stem={q.stem}
          imageUrl={q.imageUrl}
          options={q.options}
          answer={answer}
          onChange={(a) => setAnswer(a)}
          feedback={feedback && isOptionType(q.type) ? { correctOptionIds: feedback.correctOptionIds, selectedOptionIds: answer?.selectedOptionIds ?? [] } : undefined}
          disabled={!!feedback}
          label={t("questionOf", { current: index + 1, total: session.questions.length })}
        />
        {q.hint && !feedback && (
          <div className="mt-4">
            {showHint ? (
              <p className="rounded-lg bg-surface-muted px-3 py-2 text-sm">
                <strong>{t("hint")}:</strong> {q.hint}
              </p>
            ) : (
              <button type="button" onClick={() => setShowHint(true)} className="text-sm font-medium text-primary hover:underline">
                {t("hint")}
              </button>
            )}
          </div>
        )}
      </section>

      {feedback && (
        <section aria-live="polite" className={`rounded-2xl border p-5 ${feedback.outcome === "Correct" ? "border-success bg-success-soft" : feedback.outcome === "PartiallyCorrect" ? "border-warning bg-warning-soft" : "border-danger bg-danger-soft"}`}>
          <p className="text-lg font-semibold">{t(outcomeKey[feedback.outcome] ?? "incorrect")}</p>
          {!isOptionType(q.type) && feedback.correctNumericAnswer != null && <p className="mt-1">{t("correctAnswer", { answer: String(feedback.correctNumericAnswer) })}</p>}
          {feedback.explanation && (
            <div className="mt-3">
              <h2 className="font-semibold">{t("explanation")}</h2>
              <p className="mt-1 whitespace-pre-line">{feedback.explanation}</p>
            </div>
          )}
        </section>
      )}

      <StudentError error={check.error ?? finish.error} />
      <div className="flex flex-wrap items-center justify-between gap-3">
        <QuestionTools questionId={q.questionId} />
        {!feedback ? (
          <Button
            disabled={!isAnswered(answer)}
            loading={check.isPending}
            onClick={() => check.mutate({ questionId: q.questionId, response: answer!, timeSpentSeconds: Math.min(3600, Math.round((Date.now() - shownAt.current) / 1000)) })}
          >
            {t("checkAnswer")}
          </Button>
        ) : last ? (
          <Button onClick={() => finish.mutate()} loading={finish.isPending}>
            {t("finish")}
          </Button>
        ) : (
          <Button onClick={next}>
            {t("next")}
            <Icon name="chevronRight" size={16} />
          </Button>
        )}
      </div>
    </div>
  );
}
