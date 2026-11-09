"use client";

import Link from "next/link";
import { useParams, useSearchParams } from "next/navigation";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { api, unwrap } from "@/lib/api/client";
import { useT } from "@/lib/i18n";
import { Button } from "@/components/ui/button";
import { PageLoading } from "@/components/ui/feedback";
import { Icon } from "@/components/ui/icons";
import { useToast } from "@/components/ui/toast";
import { BuyButton } from "../checkout";
import { StudentError } from "../errors";
import { routes } from "../routes";
import { ProgressBar } from "../ui";

export const courseKey = (id: string) => ["student", "course", id] as const;

export function CoursePage() {
  const t = useT();
  const toast = useToast();
  const qc = useQueryClient();
  const { courseId } = useParams<{ courseId: string }>();
  // From the app's "Buy on website": open checkout at once, and point back to the app once the course is owned.
  const params = useSearchParams();
  const fromApp = params.get("from") === "app";
  const autoBuy = params.get("buy") === "1";
  const q = useQuery({ queryKey: courseKey(courseId), queryFn: () => unwrap(api.GET("/api/v1/courses/{courseId}/syllabus", { params: { path: { courseId } } })) });
  const enroll = useMutation({
    mutationFn: () => unwrap(api.POST("/api/v1/courses/{courseId}/enroll", { params: { path: { courseId } } })),
    onSuccess: async () => {
      toast(t("enrolledToast"));
      await qc.invalidateQueries({ queryKey: ["student"] });
    },
  });

  if (q.isPending) return <PageLoading />;
  if (q.isError) return <StudentError error={q.error} onRetry={() => q.refetch()} />;
  const { course, hasAccess, completionPercent, subjects } = q.data;

  return (
    <div className="space-y-6">
      <Link href={routes.learn} className="inline-flex items-center gap-1 text-sm font-medium text-primary hover:underline">
        <Icon name="chevronLeft" size={16} />
        {t("tabLearn")}
      </Link>
      <section className="rounded-2xl border border-border bg-surface p-5 sm:p-6">
        <h1 className="text-2xl font-semibold">{course.title}</h1>
        {course.description && <p className="mt-2 max-w-3xl text-fg-muted">{course.description}</p>}
        <div className="mt-5">
          {course.isFree && !course.enrolled ? (
            // Free content is open to everyone, but joining adds the course to the plan and "My courses".
            <div className="space-y-2">
              <Button onClick={() => enroll.mutate()} loading={enroll.isPending}>
                {t("enrollFree")}
              </Button>
              <StudentError error={enroll.error} />
            </div>
          ) : hasAccess ? (
            <div className="max-w-md space-y-2">
              {fromApp && (
                <p role="status" className="rounded-lg bg-success-soft px-3 py-2 text-sm font-medium text-success">
                  {t("backToApp")}
                </p>
              )}
              <p className="inline-flex items-center gap-1.5 text-sm font-medium text-success">
                <Icon name="check" size={16} />
                {t("youHaveAccess")}
              </p>
              <ProgressBar value={completionPercent} label={t("percentComplete", { percent: completionPercent })} />
              <p className="text-sm text-fg-muted">{t("percentComplete", { percent: completionPercent })}</p>
            </div>
          ) : (
            <div className="space-y-2">
              <p className="text-sm text-fg-muted">{course.accessDays ? t("accessDays", { days: course.accessDays }) : t("lifetimeAccess")}</p>
              <BuyButton course={course} autoStart={autoBuy} />
            </div>
          )}
        </div>
      </section>

      <section aria-labelledby="syllabus-heading">
        <h2 id="syllabus-heading" className="mb-3 text-lg font-semibold">
          {t("syllabus")}
        </h2>
        <div className="space-y-4">
          {subjects.map((s) => (
            <div key={s.id} className="rounded-2xl border border-border bg-surface">
              <div className="flex items-center justify-between gap-3 border-b border-border px-5 py-3">
                <h3 className="font-semibold">{s.title}</h3>
                {hasAccess && <span className="text-sm text-fg-muted">{t("percentComplete", { percent: s.completionPercent })}</span>}
              </div>
              <ul className="divide-y divide-border">
                {s.chapters.map((ch, i) => (
                  <li key={ch.id}>
                    <details open={i === 0} className="group">
                      <summary className="flex cursor-pointer list-none items-center justify-between gap-3 px-5 py-3 hover:bg-surface-muted">
                        <span>
                          {ch.unit && <span className="block text-xs text-fg-muted">{ch.unit}</span>}
                          <span className="font-medium">{ch.title}</span>
                        </span>
                        <span className="flex items-center gap-3 text-sm text-fg-muted">
                          {t("topicsCount", { count: ch.topics.length })}
                          <Icon name="chevronRight" size={16} className="transition-transform group-open:rotate-90" />
                        </span>
                      </summary>
                      <ul className="pb-2">
                        {ch.topics.map((tp) => (
                          <li key={tp.id}>
                            <Link href={routes.topic(tp.id)} className="flex items-center gap-3 px-5 py-2.5 pl-8 hover:bg-surface-muted">
                              <span className={`flex size-6 shrink-0 items-center justify-center rounded-full ${tp.completed ? "bg-success text-white" : "border border-border text-fg-muted"}`}>
                                {tp.completed ? <Icon name="check" size={14} aria-label={t("completed")} role="img" /> : null}
                              </span>
                              <span className="min-w-0 flex-1 truncate">{tp.title}</span>
                              <span className="shrink-0 text-sm text-fg-muted">
                                {tp.resourceCount > 0 && hasAccess ? `${t("lessonsDone", { done: tp.completedResources, total: tp.resourceCount })} · ` : ""}
                                {t("minutesShort", { minutes: tp.estimatedMinutes })}
                              </span>
                            </Link>
                          </li>
                        ))}
                      </ul>
                    </details>
                  </li>
                ))}
              </ul>
            </div>
          ))}
        </div>
      </section>
    </div>
  );
}
