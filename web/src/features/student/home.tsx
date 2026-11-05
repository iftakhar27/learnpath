"use client";

import Link from "next/link";
import { useQuery } from "@tanstack/react-query";
import { api, unwrap } from "@/lib/api/client";
import type { components } from "@/lib/api/schema";
import { useT } from "@/lib/i18n";
import { PageLoading } from "@/components/ui/feedback";
import { Icon, type IconName } from "@/components/ui/icons";
import { StudentError } from "./errors";
import { planTaskHref, routes } from "./routes";
import { Muted, ProgressBar, Section, useFormat } from "./ui";

type PlanTask = components["schemas"]["PlanTaskDto"];

export const dashboardKey = ["student", "dashboard"] as const;

const taskIcon: Record<string, IconName> = { Learn: "play", Practice: "pencil", Test: "clipboard", Revise: "book" };

function PlanTaskRow({ task }: { task: PlanTask }) {
  const t = useT();
  const done = task.status === "Completed";
  return (
    <li>
      <Link href={planTaskHref(task)} className="flex items-center gap-3 rounded-xl p-2 hover:bg-surface-muted">
        <span className={`rounded-lg p-2 ${done ? "bg-success-soft text-success" : "bg-primary-soft text-primary"}`}>
          <Icon name={done ? "check" : (taskIcon[task.type] ?? "book")} />
        </span>
        <span className="min-w-0 flex-1">
          <span className={`block truncate font-medium ${done ? "text-fg-muted line-through" : ""}`}>{task.title}</span>
          <span className="block truncate text-sm text-fg-muted">{task.reason}</span>
        </span>
        <span className="shrink-0 text-sm text-fg-muted">{t("minutesShort", { minutes: task.estimatedMinutes })}</span>
      </Link>
    </li>
  );
}

export function StudentHome() {
  const t = useT();
  const f = useFormat();
  const q = useQuery({ queryKey: dashboardKey, queryFn: () => unwrap(api.GET("/api/v1/student/dashboard")) });
  if (q.isPending) return <PageLoading />;
  if (q.isError) return <StudentError error={q.error} onRetry={() => q.refetch()} />;
  const d = q.data;
  const firstName = d.fullName.trim().split(/\s+/)[0];

  return (
    <div className="space-y-6">
      <div className="flex flex-wrap items-end justify-between gap-3">
        <div>
          <h1 className="text-2xl font-semibold">{t("greeting", { name: firstName })}</h1>
          {d.examName && d.daysToExam != null && <p className="mt-1 text-fg-muted">{t("daysToExam", { days: d.daysToExam, exam: `${d.examName}${d.targetYear ? ` ${d.targetYear}` : ""}` })}</p>}
        </div>
        <span className="inline-flex items-center gap-1.5 rounded-full bg-warning-soft px-3 py-1 text-sm font-medium text-warning">
          <Icon name="flame" size={16} />
          {t("streakDays", { days: d.streakDays })}
        </span>
      </div>

      <div className="grid gap-6 lg:grid-cols-3">
        <div className="space-y-6 lg:col-span-2">
          <Section title={t("todaysPlan")} action={{ href: routes.plan, label: t("viewFullPlan") }}>
            {d.todayPlan.tasks.length === 0 ? (
              <Muted>{t("planEmpty")}</Muted>
            ) : (
              <>
                <ul className="-mx-2 space-y-1">
                  {d.todayPlan.tasks.map((task) => (
                    <PlanTaskRow key={task.id} task={task} />
                  ))}
                </ul>
                <p className="mt-3 text-xs text-fg-muted">{d.todayPlan.basis}</p>
              </>
            )}
          </Section>

          {d.continueLearning.length > 0 && (
            <Section title={t("continueLearning")}>
              <ul className="grid gap-3 sm:grid-cols-2">
                {d.continueLearning.map((c) => (
                  <li key={c.resourceId}>
                    <Link href={c.type === "Video" ? routes.watch(c.resourceId) : routes.topic(c.topicId)} className="block rounded-xl border border-border p-3 hover:bg-surface-muted">
                      <span className="flex items-center gap-2 text-sm text-fg-muted">
                        <Icon name={c.type === "Video" ? "play" : "doc"} size={16} />
                        {c.topic}
                      </span>
                      <span className="mt-1 block font-medium">{c.title}</span>
                      {c.durationSeconds ? (
                        <span className="mt-2 block">
                          <ProgressBar value={(c.positionSeconds / c.durationSeconds) * 100} label={t("percentComplete", { percent: Math.round((c.positionSeconds / c.durationSeconds) * 100) })} />
                        </span>
                      ) : null}
                    </Link>
                  </li>
                ))}
              </ul>
            </Section>
          )}

          <Section title={t("myCourses")} action={{ href: routes.learn, label: t("browseCourses") }}>
            {d.courses.length === 0 ? (
              <Muted>{t("noCoursesYet")}</Muted>
            ) : (
              <ul className="space-y-3">
                {d.courses.map((c) => (
                  <li key={c.courseId}>
                    <Link href={routes.course(c.courseId)} className="block rounded-xl p-2 hover:bg-surface-muted">
                      <span className="flex items-baseline justify-between gap-3">
                        <span className="font-medium">{c.title}</span>
                        <span className="shrink-0 text-sm text-fg-muted">{t("percentComplete", { percent: c.completionPercent })}</span>
                      </span>
                      <span className="mt-2 block">
                        <ProgressBar value={c.completionPercent} label={t("percentComplete", { percent: c.completionPercent })} />
                      </span>
                    </Link>
                  </li>
                ))}
              </ul>
            )}
          </Section>
        </div>

        <div className="space-y-6">
          <Section title={t("thisWeek")}>
            <dl className="grid grid-cols-2 gap-3">
              <div className="rounded-xl bg-surface-muted p-3">
                <dt className="text-xs text-fg-muted">{t("questionsAnswered")}</dt>
                <dd className="mt-1 text-2xl font-semibold tabular-nums">{f.number(d.questionsAnsweredThisWeek)}</dd>
              </div>
              <div className="rounded-xl bg-surface-muted p-3">
                <dt className="text-xs text-fg-muted">{t("lessonsCompleted")}</dt>
                <dd className="mt-1 text-2xl font-semibold tabular-nums">{f.number(d.lessonsCompletedThisWeek)}</dd>
              </div>
            </dl>
          </Section>

          {d.weakTopics.length > 0 && (
            <Section title={t("weakTopics")}>
              <ul className="space-y-3">
                {d.weakTopics.map((w) => (
                  <li key={w.topicId} className="flex items-center justify-between gap-3">
                    <span className="min-w-0">
                      <span className="block truncate font-medium">{w.topic}</span>
                      <span className="block text-sm text-fg-muted">{t("accuracyOn", { accuracy: Math.round(w.accuracy), count: w.attempted })}</span>
                    </span>
                    <Link href={routes.practiceTopic(w.topicId)} className="shrink-0 rounded-lg border border-border px-3 py-1.5 text-sm font-medium hover:bg-surface-muted">
                      {t("practise")}
                    </Link>
                  </li>
                ))}
              </ul>
            </Section>
          )}

          {d.recentResults.length > 0 && (
            <Section title={t("recentResults")} action={{ href: routes.tests, label: t("testsTitle") }}>
              <ul className="space-y-1">
                {d.recentResults.map((r) => (
                  <li key={r.attemptId}>
                    <Link href={routes.result(r.attemptId)} className="-mx-2 flex items-center justify-between gap-3 rounded-lg p-2 hover:bg-surface-muted">
                      <span className="min-w-0">
                        <span className="block truncate font-medium">{r.test}</span>
                        <span className="block text-sm text-fg-muted">{f.date(r.submittedAt)}</span>
                      </span>
                      <span className="shrink-0 font-semibold tabular-nums">{t("scoreOf", { score: f.number(r.score), max: f.number(r.maxScore) })}</span>
                    </Link>
                  </li>
                ))}
              </ul>
            </Section>
          )}
        </div>
      </div>
    </div>
  );
}
