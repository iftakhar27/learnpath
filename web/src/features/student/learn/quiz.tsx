"use client";

import Link from "next/link";
import { useParams } from "next/navigation";
import { useEffect, useRef, useState, type ReactNode } from "react";
import { useMutation, useQueryClient } from "@tanstack/react-query";
import { api, unwrap } from "@/lib/api/client";
import type { components } from "@/lib/api/schema";
import { useT, type MessageKey } from "@/lib/i18n";
import { Button, buttonClass } from "@/components/ui/button";
import { Modal } from "@/components/ui/dialog";
import { PageLoading } from "@/components/ui/feedback";
import { Icon } from "@/components/ui/icons";
import { AskDoubtButton } from "../doubts/doubts";
import { StudentError } from "../errors";
import { QuestionView, isAnswered, isOptionType, type Answer } from "../question";
import { routes } from "../routes";
import { ProgressBar } from "../ui";

type Summary = components["schemas"]["TopicQuizSummaryDto"];
type Quiz = components["schemas"]["QuizDto"];
type Result = components["schemas"]["QuizResultDto"];

const OUTCOME: Record<string, { label: MessageKey; tone: string }> = {
  Correct: { label: "correct", tone: "border-success bg-success-soft" },
  PartiallyCorrect: { label: "partiallyCorrect", tone: "border-warning bg-warning-soft" },
  Incorrect: { label: "incorrect", tone: "border-danger bg-danger-soft" },
  Skipped: { label: "notAnswered", tone: "border-border bg-surface-muted" },
};

/** The quiz card on a topic page: highlighted once every lesson is done. */
export function TopicQuizCard({ topicId, quiz, covered }: { topicId: string; quiz: Summary; covered: boolean }) {
  const t = useT();
  return (
    <section className={`rounded-2xl border p-5 ${covered && !quiz.passed ? "border-primary bg-primary-soft" : "border-border bg-surface"}`} aria-labelledby="topic-quiz-title">
      <div className="flex flex-wrap items-start justify-between gap-3">
        <div>
          <h2 id="topic-quiz-title" className="font-semibold">
            {t("topicQuiz")}
          </h2>
          <p className="mt-0.5 text-sm text-fg-muted">{t("topicQuizInfo", { count: quiz.questionCount, percent: quiz.passPercent })}</p>
        </div>
        {quiz.attempts > 0 && (
          <span className={`rounded-full px-2.5 py-1 text-xs font-medium ${quiz.passed ? "bg-success-soft text-success" : "bg-warning-soft text-warning"}`}>
            {quiz.passed ? t("quizPassed") : t("quizNotPassed")}
            {quiz.bestPercent != null && <> · {t("quizBest", { percent: quiz.bestPercent })}</>}
          </span>
        )}
      </div>
      {quiz.attempts === 0 && <p className="mt-3 text-sm">{covered ? t("topicQuizCovered") : t("topicQuizNotCovered")}</p>}
      <Link href={routes.topicQuiz(topicId)} className={buttonClass(covered || quiz.attempts > 0 ? "primary" : "secondary", "md", "mt-4 w-full sm:w-auto")}>
        <Icon name="clipboard" size={18} />
        {quiz.attempts > 0 ? t("retakeQuiz") : t("startQuiz")}
      </Link>
    </section>
  );
}

/** Banner on a lesson page when it completes the topic. */
export function TopicDonePrompt({ topicId, quiz }: { topicId: string; quiz: Summary }) {
  const t = useT();
  return (
    <div role="status" className="flex flex-wrap items-center justify-between gap-3 rounded-2xl border border-primary bg-primary-soft px-4 py-3">
      <p className="font-medium">{t("topicDonePrompt", { count: quiz.questionCount })}</p>
      <Link href={routes.topicQuiz(topicId)} className={buttonClass("primary", "sm")}>
        {t("startQuiz")}
      </Link>
    </div>
  );
}

export function TopicQuizPage() {
  const { topicId } = useParams<{ topicId: string }>();
  // Starting picks the questions, so it runs once per visit (a retake starts a new quiz).
  const start = useMutation({ mutationFn: () => unwrap(api.POST("/api/v1/topics/{topicId}/quiz", { params: { path: { topicId } } })) });
  const started = useRef(false);
  useEffect(() => {
    if (started.current) return;
    started.current = true;
    start.mutate();
  }, [start]);

  if (start.isIdle || start.isPending) return <PageLoading />;
  if (start.isError) return <StudentError error={start.error} onRetry={() => start.mutate()} />;
  return <QuizRun key={start.data.sessionId} quiz={start.data} onRetake={() => start.mutate()} />;
}

function QuizRun({ quiz, onRetake }: { quiz: Quiz; onRetake: () => void }) {
  const t = useT();
  const qc = useQueryClient();
  const [answers, setAnswers] = useState<Record<string, Answer | null>>({});
  const [confirm, setConfirm] = useState(false);
  const [startedAt] = useState(() => Date.now());
  const submit = useMutation({
    mutationFn: () => {
      const each = Math.min(3600, Math.round((Date.now() - startedAt) / 1000 / Math.max(1, quiz.questions.length)));
      return unwrap(
        api.POST("/api/v1/practice/quizzes/{sessionId}/submit", {
          params: { path: { sessionId: quiz.sessionId } },
          body: { answers: quiz.questions.map((q) => ({ questionId: q.questionId, response: answers[q.questionId] ?? null, timeSpentSeconds: each })) },
        }),
      );
    },
    onSuccess: () => {
      setConfirm(false);
      window.scrollTo({ top: 0 });
      void qc.invalidateQueries({ queryKey: ["student"] });
    },
  });

  const unanswered = quiz.questions.filter((q) => !isAnswered(answers[q.questionId])).length;
  const back = (
    <Link href={routes.topic(quiz.topicId)} className="inline-flex items-center gap-1 text-sm font-medium text-primary hover:underline">
      <Icon name="chevronLeft" size={16} />
      {t("backToTopic")}
    </Link>
  );

  if (submit.data) return <QuizResult quiz={quiz} result={submit.data} answers={answers} back={back} onRetake={onRetake} />;

  return (
    <div className="mx-auto max-w-3xl space-y-5">
      {back}
      <div>
        <h1 className="text-2xl font-semibold">
          {t("topicQuiz")}: {quiz.topic}
        </h1>
        <p className="mt-1 text-sm text-fg-muted">{t("topicQuizInfo", { count: quiz.questions.length, percent: quiz.passPercent })}</p>
      </div>
      <ProgressBar
        value={((quiz.questions.length - unanswered) / quiz.questions.length) * 100}
        label={t("questionOf", { current: quiz.questions.length - unanswered, total: quiz.questions.length })}
      />
      <ol className="space-y-4">
        {quiz.questions.map((q, i) => (
          <li key={q.questionId} className="rounded-2xl border border-border bg-surface p-5 sm:p-6">
            <p className="mb-2 text-sm font-medium text-fg-muted">{t("questionOf", { current: i + 1, total: quiz.questions.length })}</p>
            <QuestionView
              type={q.type}
              stem={q.stem}
              imageUrl={q.imageUrl}
              options={q.options}
              answer={answers[q.questionId] ?? null}
              onChange={(a) => setAnswers((prev) => ({ ...prev, [q.questionId]: a }))}
              label={t("questionOf", { current: i + 1, total: quiz.questions.length })}
            />
          </li>
        ))}
      </ol>
      <StudentError error={submit.error} />
      <Button onClick={() => (unanswered > 0 ? setConfirm(true) : submit.mutate())} loading={submit.isPending && !confirm} className="w-full sm:w-auto">
        {t("submitQuiz")}
      </Button>
      <Modal
        open={confirm}
        onClose={() => setConfirm(false)}
        title={t("submitQuiz")}
        closeLabel={t("cancel")}
        footer={
          <>
            <Button variant="secondary" onClick={() => setConfirm(false)}>
              {t("keepWorking")}
            </Button>
            <Button onClick={() => submit.mutate()} loading={submit.isPending}>
              {t("submit")}
            </Button>
          </>
        }
      >
        <p>{t("quizUnanswered", { count: unanswered })}</p>
      </Modal>
    </div>
  );
}

function QuizResult({ quiz, result, answers, back, onRetake }: { quiz: Quiz; result: Result; answers: Record<string, Answer | null>; back: ReactNode; onRetake: () => void }) {
  const t = useT();
  const items = new Map(result.items.map((i) => [i.questionId, i]));
  return (
    <div className="mx-auto max-w-3xl space-y-5">
      {back}
      <section className={`rounded-2xl border p-6 text-center ${result.passed ? "border-success bg-success-soft" : "border-warning bg-warning-soft"}`} aria-live="polite">
        <h1 className="text-lg font-semibold">
          {t("quizResult")}: {quiz.topic}
        </h1>
        <p className="mt-2 text-5xl font-bold tabular-nums">{result.scorePercent}%</p>
        <p className="mt-2 font-medium">{result.passed ? t("quizPassedBody") : t("quizFailedBody", { percent: result.passPercent })}</p>
        <dl className={`mx-auto mt-4 grid max-w-md gap-2 text-sm ${result.partial > 0 ? "grid-cols-2 sm:grid-cols-4" : "grid-cols-3"}`}>
          {(
            [
              ["correct", result.correct],
              ...(result.partial > 0 ? [["partiallyCorrect", result.partial] as const] : []),
              ["incorrect", result.incorrect],
              ["skipped", result.skipped],
            ] as const
          ).map(([k, v]) => (
            <div key={k} className="rounded-xl bg-surface p-2">
              <dt className="text-fg-muted">{t(k)}</dt>
              <dd className="text-xl font-semibold tabular-nums">{v}</dd>
            </div>
          ))}
        </dl>
        <div className="mt-5 flex flex-wrap justify-center gap-2">
          {result.nextTopicId && result.passed && (
            <Link href={routes.topic(result.nextTopicId)} className={buttonClass("primary")}>
              {t("nextTopic")}
              <Icon name="chevronRight" size={16} />
            </Link>
          )}
          <Button variant={result.passed ? "secondary" : "primary"} onClick={onRetake}>
            {t("retakeQuiz")}
          </Button>
        </div>
      </section>

      <h2 className="text-lg font-semibold">{t("reviewAnswers")}</h2>
      <ol className="space-y-4">
        {quiz.questions.map((q, i) => {
          const item = items.get(q.questionId);
          if (!item) return null;
          const outcome = OUTCOME[item.outcome] ?? OUTCOME.Incorrect;
          const answer = answers[q.questionId] ?? null;
          return (
            <li key={q.questionId} className="space-y-3 rounded-2xl border border-border bg-surface p-5 sm:p-6">
              <div className="flex items-center justify-between gap-3">
                <p className="text-sm font-medium text-fg-muted">{t("questionOf", { current: i + 1, total: quiz.questions.length })}</p>
                <span className={`rounded-full border px-2.5 py-0.5 text-xs font-medium ${outcome.tone}`}>{t(outcome.label)}</span>
              </div>
              <QuestionView
                type={q.type}
                stem={q.stem}
                imageUrl={q.imageUrl}
                options={q.options}
                answer={answer}
                feedback={isOptionType(q.type) ? { correctOptionIds: item.correctOptionIds, selectedOptionIds: answer?.selectedOptionIds ?? [] } : undefined}
                disabled
                label={t("questionOf", { current: i + 1, total: quiz.questions.length })}
              />
              {!isOptionType(q.type) && item.correctNumericAnswer != null && (
                <p className="text-sm">{t("correctAnswer", { answer: String(item.correctNumericAnswer) })}</p>
              )}
              {item.explanation && (
                <div className="rounded-xl bg-surface-muted p-3 text-sm">
                  <p className="font-semibold">{t("explanation")}</p>
                  <p className="mt-1 whitespace-pre-line">{item.explanation}</p>
                </div>
              )}
              <AskDoubtButton link={{ questionId: q.questionId }} about={q.stem} />
            </li>
          );
        })}
      </ol>
    </div>
  );
}
