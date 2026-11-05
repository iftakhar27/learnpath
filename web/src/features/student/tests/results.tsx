"use client";

import Link from "next/link";
import { useParams } from "next/navigation";
import { useState } from "react";
import { useQuery } from "@tanstack/react-query";
import { api, unwrap } from "@/lib/api/client";
import type { components } from "@/lib/api/schema";
import { useT, type MessageKey } from "@/lib/i18n";
import { buttonClass } from "@/components/ui/button";
import { PageLoading } from "@/components/ui/feedback";
import { Icon } from "@/components/ui/icons";
import { StudentError } from "../errors";
import { QuestionTools } from "../practice/report";
import { QuestionView, isOptionType } from "../question";
import { routes } from "../routes";
import { ProgressBar, useFormat } from "../ui";

type Breakdown = components["schemas"]["BreakdownDto"];
export const resultKey = (attemptId: string) => ["student", "result", attemptId] as const;

function useResult() {
  const { attemptId } = useParams<{ attemptId: string }>();
  return useQuery({ queryKey: resultKey(attemptId), queryFn: () => unwrap(api.GET("/api/v1/attempts/{attemptId}/results", { params: { path: { attemptId } } })) });
}

function BreakdownTable({ title, rows }: { title: string; rows: Breakdown[] }) {
  const t = useT();
  const f = useFormat();
  if (rows.length === 0) return null;
  return (
    <section className="rounded-2xl border border-border bg-surface p-5">
      <h2 className="mb-3 font-semibold">{title}</h2>
      <ul className="space-y-3">
        {rows.map((r) => (
          <li key={r.id}>
            <div className="flex items-baseline justify-between gap-3 text-sm">
              <span className="font-medium">{r.name}</span>
              <span className="tabular-nums text-fg-muted">
                {t("scoreOf", { score: f.number(r.score), max: f.number(r.maxScore) })} · {t("accuracy")} {Math.round(r.accuracy)}%
              </span>
            </div>
            <div className="mt-1.5">
              <ProgressBar value={r.maxScore > 0 ? (Math.max(0, r.score) / r.maxScore) * 100 : 0} label={r.name} />
            </div>
          </li>
        ))}
      </ul>
    </section>
  );
}

export function ResultPage() {
  const t = useT();
  const f = useFormat();
  const q = useResult();
  if (q.isPending) return <PageLoading />;
  if (q.isError) return <StudentError error={q.error} onRetry={() => q.refetch()} />;
  const r = q.data;
  const stats: [MessageKey, number][] = [
    ["correct", r.correct],
    ["incorrect", r.incorrect],
    ["partiallyCorrect", r.partial],
    ["skipped", r.skipped],
  ];

  return (
    <div className="mx-auto max-w-4xl space-y-6">
      <Link href={routes.tests} className="inline-flex items-center gap-1 text-sm font-medium text-primary hover:underline">
        <Icon name="chevronLeft" size={16} />
        {t("backToTests")}
      </Link>
      <section className="rounded-2xl border border-border bg-surface p-5 sm:p-6">
        <p className="text-sm text-fg-muted">
          {t("resultTitle")} · {t("attemptNumber", { number: r.attemptNumber })}
        </p>
        <h1 className="mt-1 text-2xl font-semibold">{r.testTitle}</h1>
        {r.status === "AutoSubmitted" && <p className="mt-2 text-sm text-warning">{t("autoSubmitted")}</p>}
        <div className="mt-5 flex flex-wrap items-end gap-x-8 gap-y-4">
          <div>
            <p className="text-sm text-fg-muted">{t("score")}</p>
            <p className="text-4xl font-semibold tabular-nums">{t("scoreOf", { score: f.number(r.score), max: f.number(r.maxScore) })}</p>
          </div>
          <div>
            <p className="text-sm text-fg-muted">{t("accuracy")}</p>
            <p className="text-2xl font-semibold tabular-nums">{Math.round(r.accuracy)}%</p>
          </div>
          <div>
            <p className="text-sm text-fg-muted">{t("timeTaken")}</p>
            <p className="text-2xl font-semibold tabular-nums">{f.duration(r.timeTakenSeconds)}</p>
          </div>
        </div>
        <dl className="mt-5 grid grid-cols-2 gap-3 sm:grid-cols-4">
          {stats.map(([k, v]) => (
            <div key={k} className="rounded-xl bg-surface-muted p-3">
              <dt className="text-xs text-fg-muted">{t(k)}</dt>
              <dd className="mt-1 text-xl font-semibold tabular-nums">{v}</dd>
            </div>
          ))}
        </dl>
        {r.percentile && (
          <p className="mt-4 text-sm">
            {t("percentileLine", { percent: Math.round(r.percentile.percentile) })} <span className="text-fg-muted">{t("notOfficialRank")}</span>
          </p>
        )}
        <div className="mt-6 flex flex-wrap gap-2">
          <Link href={routes.review(r.attemptId)} className={buttonClass("primary")}>
            {t("reviewAnswers")}
          </Link>
          <Link href={routes.test(r.testId)} className={buttonClass("secondary")}>
            {t("retakeTest")}
          </Link>
        </div>
      </section>

      {r.recommendations.length > 0 && (
        <section className="rounded-2xl border border-border bg-surface p-5">
          <h2 className="mb-2 font-semibold">{t("recommendations")}</h2>
          <ul className="list-disc space-y-1 pl-5">
            {r.recommendations.map((rec) => (
              <li key={rec}>{rec}</li>
            ))}
          </ul>
        </section>
      )}
      <div className="grid gap-6 md:grid-cols-2">
        <BreakdownTable title={t("subjectWise")} rows={r.subjects} />
        <BreakdownTable title={t("chapterWise")} rows={r.chapters} />
      </div>
    </div>
  );
}

type Filter = "all" | "incorrect" | "skipped";

export function ReviewPage() {
  const t = useT();
  const f = useFormat();
  const q = useResult();
  const [filter, setFilter] = useState<Filter>("all");
  if (q.isPending) return <PageLoading />;
  if (q.isError) return <StudentError error={q.error} onRetry={() => q.refetch()} />;
  const r = q.data;
  const questions = r.questions.filter((x) => filter === "all" || (filter === "incorrect" ? x.outcome === "Incorrect" || x.outcome === "PartiallyCorrect" : x.outcome === "Skipped"));
  const outcomeKey: Record<string, MessageKey> = { Correct: "correct", Incorrect: "incorrect", PartiallyCorrect: "partiallyCorrect", Skipped: "skipped" };
  const outcomeTone: Record<string, string> = { Correct: "bg-success-soft text-success", Incorrect: "bg-danger-soft text-danger", PartiallyCorrect: "bg-warning-soft text-warning", Skipped: "bg-surface-muted text-fg-muted" };

  return (
    <div className="mx-auto max-w-3xl space-y-6">
      <Link href={routes.result(r.attemptId)} className="inline-flex items-center gap-1 text-sm font-medium text-primary hover:underline">
        <Icon name="chevronLeft" size={16} />
        {t("resultTitle")}
      </Link>
      <div className="flex flex-wrap items-center justify-between gap-3">
        <h1 className="text-2xl font-semibold">{t("reviewAnswers")}</h1>
        <div role="radiogroup" aria-label={t("reviewAnswers")} className="flex gap-1 rounded-lg bg-surface-muted p-1 text-sm">
          {(
            [
              ["all", "filterAll"],
              ["incorrect", "filterIncorrect"],
              ["skipped", "filterSkipped"],
            ] as const
          ).map(([v, k]) => (
            <button
              key={v}
              type="button"
              role="radio"
              aria-checked={filter === v}
              onClick={() => setFilter(v)}
              className={`rounded-md px-3 py-1.5 font-medium ${filter === v ? "bg-surface text-fg shadow-sm" : "text-fg-muted hover:text-fg"}`}
            >
              {t(k)}
            </button>
          ))}
        </div>
      </div>
      {questions.length === 0 && <p className="text-fg-muted">{t("emptyGeneric")}</p>}
      <ol className="space-y-4">
        {questions.map((x) => (
          <li key={x.testQuestionId} className="rounded-2xl border border-border bg-surface p-5">
            <div className="mb-3 flex flex-wrap items-center justify-between gap-2 text-sm">
              <span className="text-fg-muted">
                {t("questionOf", { current: x.number, total: r.questions.length })} · {x.chapter}
              </span>
              <span className="flex items-center gap-2">
                <span className={`rounded-full px-2 py-0.5 text-xs font-medium ${outcomeTone[x.outcome] ?? ""}`}>{t(outcomeKey[x.outcome] ?? "skipped")}</span>
                <span className="font-medium tabular-nums">{t("marksAwarded", { marks: f.number(x.marksAwarded) })}</span>
              </span>
            </div>
            <QuestionView
              type={x.type}
              stem={x.stem}
              imageUrl={x.imageUrl}
              options={x.options}
              answer={null}
              feedback={isOptionType(x.type) ? { correctOptionIds: x.options.filter((o) => o.isCorrect).map((o) => o.id), selectedOptionIds: x.options.filter((o) => o.selected).map((o) => o.id) } : undefined}
              disabled
            />
            {!isOptionType(x.type) && (
              <div className="mt-3 space-y-1 text-sm">
                <p>{t("yourAnswer", { answer: x.yourNumericAnswer != null ? String(x.yourNumericAnswer) : t("notAnswered") })}</p>
                {x.correctNumericAnswer != null && <p className="font-medium text-success">{t("correctAnswer", { answer: String(x.correctNumericAnswer) })}</p>}
              </div>
            )}
            {x.explanation && (
              <div className="mt-4 rounded-xl bg-surface-muted p-4">
                <h2 className="font-semibold">{t("explanation")}</h2>
                <p className="mt-1 whitespace-pre-line">{x.explanation}</p>
              </div>
            )}
            <div className="mt-4">
              <QuestionTools questionId={x.questionId} />
            </div>
          </li>
        ))}
      </ol>
    </div>
  );
}
