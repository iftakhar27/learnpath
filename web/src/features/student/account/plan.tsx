"use client";

import Link from "next/link";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { api, unwrap } from "@/lib/api/client";
import { useT } from "@/lib/i18n";
import { Button } from "@/components/ui/button";
import { PageLoading } from "@/components/ui/feedback";
import { Icon } from "@/components/ui/icons";
import { StudentError } from "../errors";
import { planTaskHref } from "../routes";
import { Muted, PageTitle, Section, useFormat } from "../ui";

export function PlanPage() {
  const t = useT();
  const f = useFormat();
  const qc = useQueryClient();
  const plan = useQuery({ queryKey: ["student", "plan"], queryFn: () => unwrap(api.GET("/api/v1/student/study-plan")) });
  const history = useQuery({ queryKey: ["student", "plan-history"], queryFn: () => unwrap(api.GET("/api/v1/student/study-plan/history", { params: { query: { days: 7 } } })) });
  const refresh = () => qc.invalidateQueries({ queryKey: ["student"] });
  const regenerate = useMutation({ mutationFn: () => unwrap(api.POST("/api/v1/student/study-plan/regenerate")), onSuccess: refresh });
  const setStatus = useMutation({
    mutationFn: ({ id, action }: { id: string; action: "complete" | "skip" }) =>
      action === "complete"
        ? unwrap(api.POST("/api/v1/student/study-plan/tasks/{taskId}/complete", { params: { path: { taskId: id } } }))
        : unwrap(api.POST("/api/v1/student/study-plan/tasks/{taskId}/skip", { params: { path: { taskId: id } } })),
    onSuccess: refresh,
  });

  if (plan.isPending) return <PageLoading />;
  if (plan.isError) return <StudentError error={plan.error} onRetry={() => plan.refetch()} />;
  const p = plan.data;

  return (
    <div className="mx-auto max-w-3xl space-y-6">
      <PageTitle
        subtitle={t("plannedMinutes", { planned: p.plannedMinutes, daily: p.dailyStudyMinutes })}
        actions={
          <Button variant="secondary" onClick={() => regenerate.mutate()} loading={regenerate.isPending}>
            {t("regeneratePlan")}
          </Button>
        }
      >
        {t("planTitle")}
      </PageTitle>
      <StudentError error={regenerate.error ?? setStatus.error} />
      <Section title={t("todaysPlan")}>
        {p.tasks.length === 0 ? (
          <Muted>{t("planEmpty")}</Muted>
        ) : (
          <ul className="divide-y divide-border">
            {p.tasks.map((task) => {
              const done = task.status === "Completed";
              const skipped = task.status === "Skipped";
              return (
                <li key={task.id} className="flex flex-wrap items-center gap-3 py-3">
                  <Link href={planTaskHref(task)} className="min-w-0 flex-1">
                    <span className={`block font-medium hover:underline ${done || skipped ? "text-fg-muted line-through" : ""}`}>{task.title}</span>
                    <span className="block text-sm text-fg-muted">
                      {task.reason} · {t("minutesShort", { minutes: task.estimatedMinutes })}
                    </span>
                  </Link>
                  {done ? (
                    <span className="inline-flex items-center gap-1 text-sm font-medium text-success">
                      <Icon name="check" size={16} />
                      {t("completed")}
                    </span>
                  ) : skipped ? (
                    <span className="text-sm text-fg-muted">{t("skipped")}</span>
                  ) : (
                    <span className="flex gap-2">
                      <Button size="sm" variant="secondary" onClick={() => setStatus.mutate({ id: task.id, action: "skip" })} aria-label={`${t("skip")}: ${task.title}`}>
                        {t("skip")}
                      </Button>
                      <Button size="sm" onClick={() => setStatus.mutate({ id: task.id, action: "complete" })} aria-label={`${t("done")}: ${task.title}`}>
                        {t("done")}
                      </Button>
                    </span>
                  )}
                </li>
              );
            })}
          </ul>
        )}
        <p className="mt-3 text-xs text-fg-muted">{p.basis}</p>
      </Section>
      {history.data && history.data.length > 0 && (
        <Section title={t("lastSevenDays")}>
          <ul className="grid grid-cols-2 gap-2 sm:grid-cols-4">
            {history.data.map((d) => (
              <li key={d.date} className="rounded-xl bg-surface-muted p-3 text-sm">
                <span className="block text-fg-muted">{f.date(d.date)}</span>
                <span className="font-medium tabular-nums">{t("tasksDone", { done: d.completed, total: d.planned })}</span>
              </li>
            ))}
          </ul>
        </Section>
      )}
    </div>
  );
}
