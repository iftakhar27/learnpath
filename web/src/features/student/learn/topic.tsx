"use client";

import Link from "next/link";
import { useParams } from "next/navigation";
import { useState } from "react";
import { useQuery, useQueryClient } from "@tanstack/react-query";
import { api, unwrap } from "@/lib/api/client";
import type { components } from "@/lib/api/schema";
import { useT } from "@/lib/i18n";
import { buttonClass } from "@/components/ui/button";
import { PageLoading } from "@/components/ui/feedback";
import { Icon } from "@/components/ui/icons";
import { StudentError } from "../errors";
import { routes } from "../routes";
import { ProgressBar, useFormat } from "../ui";
import { useBookmark } from "./bookmarks";

type Resource = components["schemas"]["ResourceDto"];
export const topicKey = (id: string) => ["student", "topic", id] as const;

function ResourceRow({ r, onError }: { r: Resource; onError: (e: unknown) => void }) {
  const t = useT();
  const f = useFormat();
  const qc = useQueryClient();
  const [opening, setOpening] = useState(false);
  const video = r.type === "Video";
  const pct = r.durationSeconds ? Math.min(100, (r.positionSeconds / r.durationSeconds) * 100) : 0;

  const badges = (
    <span className="flex flex-wrap items-center gap-2 text-xs">
      {r.completed && (
        <span className="inline-flex items-center gap-1 rounded-full bg-success-soft px-2 py-0.5 font-medium text-success">
          <Icon name="check" size={12} />
          {t("completed")}
        </span>
      )}
      {r.isFreePreview && !r.completed && <span className="rounded-full bg-primary-soft px-2 py-0.5 font-medium text-primary">{t("freePreview")}</span>}
      {r.locked && (
        <span className="inline-flex items-center gap-1 rounded-full bg-surface-muted px-2 py-0.5 font-medium text-fg-muted">
          <Icon name="lock" size={12} />
          {t("locked")}
        </span>
      )}
    </span>
  );
  const body = (
    <>
      <span className={`rounded-xl p-2.5 ${r.locked ? "bg-surface-muted text-fg-muted" : "bg-primary-soft text-primary"}`}>
        <Icon name={r.locked ? "lock" : video ? "play" : "doc"} />
      </span>
      <span className="min-w-0 flex-1">
        <span className="block font-medium">{r.title}</span>
        <span className="mt-0.5 flex flex-wrap items-center gap-2 text-sm text-fg-muted">
          {video ? t("video") : t("notes")}
          {r.durationSeconds ? <span>· {f.duration(r.durationSeconds)}</span> : null}
          {badges}
        </span>
        {video && pct > 0 && !r.completed && (
          <span className="mt-2 block max-w-xs">
            <ProgressBar value={pct} label={t("percentComplete", { percent: Math.round(pct) })} />
          </span>
        )}
      </span>
    </>
  );

  if (r.locked)
    return (
      <div className="flex items-center gap-3 rounded-xl border border-border p-3 opacity-80" aria-disabled>
        {body}
      </div>
    );
  if (video)
    return (
      <Link href={routes.watch(r.id)} className="flex items-center gap-3 rounded-xl border border-border p-3 hover:border-primary">
        {body}
        <Icon name="chevronRight" className="text-fg-muted" />
      </Link>
    );

  // Notes open in a new tab via a short-lived signed link, and count as done once opened.
  const openNotes = async () => {
    const tab = window.open("about:blank", "_blank");
    setOpening(true);
    try {
      const playback = await unwrap(api.GET("/api/v1/resources/{resourceId}/playback", { params: { path: { resourceId: r.id } } }));
      if (tab) {
        tab.opener = null;
        tab.location.href = playback.url;
      } else window.location.href = playback.url;
      await unwrap(api.POST("/api/v1/learning/progress", { body: { resourceId: r.id, positionSeconds: 0, markCompleted: true } }));
      await qc.invalidateQueries({ queryKey: ["student"] });
    } catch (e) {
      tab?.close();
      onError(e);
    } finally {
      setOpening(false);
    }
  };
  return (
    <button type="button" onClick={openNotes} disabled={opening} className="flex w-full items-center gap-3 rounded-xl border border-border p-3 text-left hover:border-primary disabled:opacity-60">
      {body}
      <span className="shrink-0 text-sm font-medium text-primary">{t("openNotes")}</span>
    </button>
  );
}

export function TopicPage() {
  const t = useT();
  const { topicId } = useParams<{ topicId: string }>();
  const q = useQuery({ queryKey: topicKey(topicId), queryFn: () => unwrap(api.GET("/api/v1/topics/{topicId}", { params: { path: { topicId } } })) });
  const bookmark = useBookmark("Topic", topicId);
  const [error, setError] = useState<unknown>(null);

  if (q.isPending) return <PageLoading />;
  if (q.isError) return <StudentError error={q.error} onRetry={() => q.refetch()} />;
  const topic = q.data;

  return (
    <div className="mx-auto max-w-3xl space-y-6">
      <div>
        <Link href={routes.course(topic.courseId)} className="inline-flex items-center gap-1 text-sm font-medium text-primary hover:underline">
          <Icon name="chevronLeft" size={16} />
          {t("viewCourse")}
        </Link>
        <p className="mt-3 text-sm text-fg-muted">
          {topic.subject} · {topic.chapter}
        </p>
        <div className="mt-1 flex items-start justify-between gap-3">
          <h1 className="text-2xl font-semibold">{topic.title}</h1>
          <button
            type="button"
            onClick={() => bookmark.toggle.mutate()}
            disabled={!bookmark.ready || bookmark.toggle.isPending}
            aria-pressed={bookmark.saved}
            className={`inline-flex shrink-0 items-center gap-1.5 rounded-lg border px-3 py-1.5 text-sm font-medium ${bookmark.saved ? "border-primary bg-primary-soft text-primary" : "border-border hover:bg-surface-muted"}`}
          >
            <Icon name="bookmark" size={16} fill={bookmark.saved ? "currentColor" : "none"} />
            {bookmark.saved ? t("bookmarked") : t("bookmark")}
          </button>
        </div>
        <p className="mt-1 text-sm text-fg-muted">{t("minutesShort", { minutes: topic.estimatedMinutes })}</p>
      </div>

      {!topic.hasAccess && (
        <p className="flex flex-wrap items-center justify-between gap-3 rounded-xl bg-warning-soft px-4 py-3 text-sm text-warning">
          {t("unlockToContinue")}
          <Link href={routes.course(topic.courseId)} className="font-medium underline">
            {t("viewCourse")}
          </Link>
        </p>
      )}

      {topic.objectives && (
        <section className="rounded-2xl border border-border bg-surface p-5">
          <h2 className="font-semibold">{t("objectives")}</h2>
          <p className="mt-2 whitespace-pre-line text-fg-muted">{topic.objectives}</p>
        </section>
      )}

      <section className="space-y-3">
        <StudentError error={error} />
        {topic.resources.map((r) => (
          <ResourceRow key={r.id} r={r} onError={setError} />
        ))}
      </section>

      {topic.hasAccess && (
        <section className="rounded-2xl border border-border bg-surface p-5">
          {topic.practiceQuestionCount > 0 ? (
            <Link href={routes.practiceTopic(topic.id)} className={buttonClass("primary", "md", "w-full sm:w-auto")}>
              <Icon name="pencil" size={18} />
              {t("practiseTopic", { count: topic.practiceQuestionCount })}
            </Link>
          ) : (
            <p className="text-sm text-fg-muted">{t("noPracticeQuestions")}</p>
          )}
        </section>
      )}

      <nav className="flex justify-between gap-3" aria-label={t("syllabus")}>
        {topic.previousTopicId ? (
          <Link href={routes.topic(topic.previousTopicId)} className={buttonClass("secondary")}>
            <Icon name="chevronLeft" size={16} />
            {t("previousTopic")}
          </Link>
        ) : (
          <span />
        )}
        {topic.nextTopicId && (
          <Link href={routes.topic(topic.nextTopicId)} className={buttonClass("secondary")}>
            {t("nextTopic")}
            <Icon name="chevronRight" size={16} />
          </Link>
        )}
      </nav>
    </div>
  );
}
