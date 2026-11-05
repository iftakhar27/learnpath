"use client";

import Link from "next/link";
import { useParams, useRouter } from "next/navigation";
import { useMutation, useQuery } from "@tanstack/react-query";
import { api, unwrap } from "@/lib/api/client";
import type { components } from "@/lib/api/schema";
import { useT } from "@/lib/i18n";
import { Button, buttonClass } from "@/components/ui/button";
import { PageLoading, SkeletonRows } from "@/components/ui/feedback";
import { Icon } from "@/components/ui/icons";
import { StudentError } from "../errors";
import { routes } from "../routes";
import { Muted, PageTitle, useFormat } from "../ui";

type TestSummary = components["schemas"]["TestSummaryDto"];

function TestFacts({ test }: { test: TestSummary }) {
  const t = useT();
  const f = useFormat();
  return (
    <span className="flex flex-wrap gap-x-3 gap-y-1 text-sm text-fg-muted">
      <span>{t("questionsCount", { count: test.questionCount })}</span>
      <span>{t("minutesCount", { minutes: test.durationMinutes })}</span>
      <span>{t("marksCount", { marks: f.number(test.maxMarks) })}</span>
      {test.maxAttempts != null && <span>{t("attemptsUsed", { used: test.attemptsUsed, max: test.maxAttempts })}</span>}
    </span>
  );
}

export function TestsList() {
  const t = useT();
  const q = useQuery({ queryKey: ["student", "tests"], queryFn: () => unwrap(api.GET("/api/v1/tests")) });
  return (
    <>
      <PageTitle>{t("testsTitle")}</PageTitle>
      {q.isPending ? (
        <SkeletonRows rows={3} />
      ) : q.isError ? (
        <StudentError error={q.error} onRetry={() => q.refetch()} />
      ) : q.data.length === 0 ? (
        <Muted>{t("testsEmpty")}</Muted>
      ) : (
        <ul className="grid gap-4 md:grid-cols-2">
          {q.data.map((test) => (
            <li key={test.id} className="flex flex-col rounded-2xl border border-border bg-surface p-5">
              <span className="flex flex-wrap items-center gap-2 text-xs font-medium">
                <span className="rounded-full bg-surface-muted px-2 py-0.5 text-fg-muted">{test.type}</span>
                {test.isFree && <span className="rounded-full bg-primary-soft px-2 py-0.5 text-primary">{t("free")}</span>}
                {test.locked && (
                  <span className="inline-flex items-center gap-1 rounded-full bg-surface-muted px-2 py-0.5 text-fg-muted">
                    <Icon name="lock" size={12} />
                    {t("locked")}
                  </span>
                )}
              </span>
              <h2 className="mt-2 text-lg font-semibold">{test.title}</h2>
              <div className="mt-1">
                <TestFacts test={test} />
              </div>
              <div className="mt-auto flex flex-wrap gap-2 pt-4">
                <Link href={routes.test(test.id)} className={buttonClass(test.locked ? "secondary" : "primary", "sm")} aria-label={`${test.inProgressAttemptId ? t("resumeTest") : test.attemptsUsed > 0 ? t("retakeTest") : t("startTest")}: ${test.title}`}>
                  {test.inProgressAttemptId ? t("resumeTest") : test.attemptsUsed > 0 ? t("retakeTest") : t("startTest")}
                </Link>
                {test.lastAttemptId && (
                  <Link href={routes.result(test.lastAttemptId)} className={buttonClass("secondary", "sm")}>
                    {t("viewResult")}
                  </Link>
                )}
              </div>
            </li>
          ))}
        </ul>
      )}
    </>
  );
}

/** Instructions, rules and previous attempts; starting (or resuming) opens the exam player. */
export function TestInstructions() {
  const t = useT();
  const f = useFormat();
  const router = useRouter();
  const { testId } = useParams<{ testId: string }>();
  const q = useQuery({ queryKey: ["student", "test", testId], queryFn: () => unwrap(api.GET("/api/v1/tests/{testId}", { params: { path: { testId } } })) });
  const history = useQuery({ queryKey: ["student", "attempts", testId], queryFn: () => unwrap(api.GET("/api/v1/attempts", { params: { query: { testId } } })) });
  const start = useMutation({
    mutationFn: () => unwrap(api.POST("/api/v1/tests/{testId}/attempts", { params: { path: { testId } } })),
    onSuccess: (attempt) => router.push(routes.exam(attempt.attemptId)),
  });

  if (q.isPending) return <PageLoading />;
  if (q.isError) return <StudentError error={q.error} onRetry={() => q.refetch()} />;
  const { summary, instructions, sections, negativeMarksPerWrong } = q.data;
  const outOfAttempts = summary.maxAttempts != null && summary.attemptsUsed >= summary.maxAttempts && !summary.inProgressAttemptId;

  return (
    <div className="mx-auto max-w-3xl space-y-6">
      <Link href={routes.tests} className="inline-flex items-center gap-1 text-sm font-medium text-primary hover:underline">
        <Icon name="chevronLeft" size={16} />
        {t("testsTitle")}
      </Link>
      <section className="rounded-2xl border border-border bg-surface p-5 sm:p-6">
        <h1 className="text-2xl font-semibold">{summary.title}</h1>
        <div className="mt-2">
          <TestFacts test={summary} />
        </div>
        <h2 className="mt-6 font-semibold">{t("instructions")}</h2>
        <ul className="mt-2 list-disc space-y-1 pl-5 text-fg-muted">
          <li>{negativeMarksPerWrong ? t("negativeMarking", { marks: f.number(negativeMarksPerWrong) }) : t("noNegativeMarking")}</li>
          {sections.length > 0 && (
            <li>
              {t("sections")}: {sections.join(", ")}
            </li>
          )}
          <li>{t("testRules")}</li>
        </ul>
        {instructions && <p className="mt-3 whitespace-pre-line">{instructions}</p>}
        <div className="mt-6 space-y-2">
          {summary.locked ? (
            <p className="rounded-lg bg-warning-soft px-3 py-2 text-sm text-warning">{t("purchaseRequired")}</p>
          ) : (
            <Button onClick={() => start.mutate()} loading={start.isPending} disabled={outOfAttempts}>
              {summary.inProgressAttemptId ? t("resumeTest") : t("startTest")}
            </Button>
          )}
          <StudentError error={start.error} />
        </div>
      </section>

      {history.data && history.data.length > 0 && (
        <section className="rounded-2xl border border-border bg-surface p-5">
          <h2 className="mb-2 font-semibold">{t("recentResults")}</h2>
          <ul className="divide-y divide-border">
            {history.data.map((a) => (
              <li key={a.attemptId}>
                <Link href={a.status === "InProgress" ? routes.exam(a.attemptId) : routes.result(a.attemptId)} className="flex items-center justify-between gap-3 py-2.5 hover:text-primary">
                  <span>
                    <span className="block font-medium">{t("attemptNumber", { number: a.attemptNumber })}</span>
                    <span className="block text-sm text-fg-muted">{f.dateTime(a.submittedAt ?? a.startedAt)}</span>
                  </span>
                  <span className="font-semibold tabular-nums">{a.score != null ? t("scoreOf", { score: f.number(a.score), max: f.number(a.maxScore) }) : t("resumeTest")}</span>
                </Link>
              </li>
            ))}
          </ul>
        </section>
      )}
    </div>
  );
}
