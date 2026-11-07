"use client";

import Link from "next/link";
import { useParams } from "next/navigation";
import { useCallback, useEffect, useRef, useState } from "react";
import { useQuery, useQueryClient } from "@tanstack/react-query";
import { api, unwrap } from "@/lib/api/client";
import type { components } from "@/lib/api/schema";
import { useT } from "@/lib/i18n";
import { PageLoading } from "@/components/ui/feedback";
import { Icon } from "@/components/ui/icons";
import { AskDoubtButton } from "../doubts/doubts";
import { StudentError } from "../errors";
import { routes } from "../routes";
import { useFormat } from "../ui";
import { TopicDonePrompt } from "./quiz";

type Playback = components["schemas"]["PlaybackDto"];
type Progress = components["schemas"]["ProgressDto"];
const SAVE_EVERY_MS = 10_000;

/** Best-effort progress save; `keepalive` lets the last save finish while the page unloads. */
async function saveProgress(resourceId: string, positionSeconds: number, keepalive = false): Promise<Progress | null> {
  try {
    const res = await fetch("/api/v1/learning/progress", {
      method: "POST",
      headers: { "content-type": "application/json" },
      body: JSON.stringify({ resourceId, positionSeconds: Math.floor(positionSeconds) }),
      keepalive,
    });
    return res.ok ? ((await res.json()) as Progress) : null;
  } catch {
    return null;
  }
}

function Player({ playback, onCompleted }: { playback: Playback; onCompleted: (progress: Progress) => void }) {
  const t = useT();
  const f = useFormat();
  const ref = useRef<HTMLVideoElement>(null);
  const [failed, setFailed] = useState(false);
  const [resumedAt, setResumedAt] = useState<number | null>(null);
  const src = playback.streamUrl!;

  // Attach the stream: HLS through hls.js where the browser can't play it natively (everything but Safari).
  useEffect(() => {
    const video = ref.current!;
    let destroyed = false;
    let hls: { destroy: () => void } | null = null;
    const isHls = new URL(src).pathname.endsWith(".m3u8");
    if (isHls && !video.canPlayType("application/vnd.apple.mpegurl")) {
      void import("hls.js").then(({ default: Hls }) => {
        if (destroyed) return;
        if (!Hls.isSupported()) return setFailed(true);
        const h = new Hls();
        h.on(Hls.Events.ERROR, (_e, data) => data.fatal && setFailed(true));
        h.loadSource(src);
        h.attachMedia(video);
        hls = h;
      });
    } else {
      video.src = src;
    }
    return () => {
      destroyed = true;
      hls?.destroy();
    };
  }, [src]);

  // Resume, and save progress every 10 s while playing, on pause/end, and when leaving.
  useEffect(() => {
    const video = ref.current!;
    let lastSaved = -1;
    const save = async (keepalive = false) => {
      const pos = Math.floor(video.currentTime);
      if (pos === lastSaved || pos === 0) return;
      lastSaved = pos;
      const progress = await saveProgress(playback.resourceId, pos, keepalive);
      if (progress?.completed) onCompleted(progress);
    };
    const onMeta = () => {
      const at = playback.resumeAtSeconds;
      if (at > 5 && (!video.duration || at < video.duration - 5)) {
        video.currentTime = at;
        setResumedAt(at);
      }
    };
    const onPause = () => void save();
    const onError = () => setFailed(true);
    const timer = setInterval(() => !video.paused && void save(), SAVE_EVERY_MS);
    const onHide = () => document.visibilityState === "hidden" && void save(true);
    video.addEventListener("loadedmetadata", onMeta);
    video.addEventListener("pause", onPause);
    video.addEventListener("ended", onPause);
    video.addEventListener("error", onError);
    document.addEventListener("visibilitychange", onHide);
    return () => {
      void save(true);
      clearInterval(timer);
      video.removeEventListener("loadedmetadata", onMeta);
      video.removeEventListener("pause", onPause);
      video.removeEventListener("ended", onPause);
      video.removeEventListener("error", onError);
      document.removeEventListener("visibilitychange", onHide);
    };
  }, [playback.resourceId, playback.resumeAtSeconds, onCompleted]);

  if (failed) return <VideoUnavailable />;
  return (
    <div className="space-y-2">
      <video ref={ref} controls playsInline preload="metadata" className="aspect-video w-full rounded-2xl bg-black" aria-label={playback.title} />
      {resumedAt != null && (
        <p role="status" className="text-sm text-fg-muted">
          {t("resumedAt", { time: f.duration(resumedAt) })}
        </p>
      )}
    </div>
  );
}

function VideoUnavailable() {
  const t = useT();
  return (
    <div className="flex aspect-video w-full flex-col items-center justify-center gap-2 rounded-2xl bg-surface-muted p-6 text-center text-fg-muted">
      <Icon name="play" size={32} />
      <p>{t("videoUnavailable")}</p>
    </div>
  );
}

export function WatchPage() {
  const t = useT();
  const qc = useQueryClient();
  const { resourceId } = useParams<{ resourceId: string }>();
  const q = useQuery({
    queryKey: ["student", "playback", resourceId],
    queryFn: () => unwrap(api.GET("/api/v1/resources/{resourceId}/playback", { params: { path: { resourceId } } })),
    staleTime: 60 * 60_000, // signed for 2 hours
    refetchOnMount: "always",
  });
  const [completedNow, setCompletedNow] = useState<Progress | null>(null);
  // Stable, so the player's progress listeners aren't re-attached on every render.
  const markCompleted = useCallback((progress: Progress) => setCompletedNow(progress), []);

  // When leaving, topic/course/home progress should reflect what was watched.
  useEffect(() => () => void qc.invalidateQueries({ predicate: (x) => x.queryKey[0] === "student" && x.queryKey[1] !== "playback" }), [qc]);

  if (q.isPending) return <PageLoading />;
  if (q.isError) return <StudentError error={q.error} onRetry={() => q.refetch()} />;
  const p = q.data;
  const completed = p.completed || !!completedNow;
  const quiz = completedNow?.topicCompleted ? completedNow.quiz : null;

  return (
    <div className="mx-auto max-w-4xl space-y-4">
      <Link href={routes.topic(p.topicId)} className="inline-flex items-center gap-1 text-sm font-medium text-primary hover:underline">
        <Icon name="chevronLeft" size={16} />
        {t("backToTopic")}
      </Link>
      {p.streamUrl ? <Player playback={p} onCompleted={markCompleted} /> : <VideoUnavailable />}
      <div className="flex flex-wrap items-start justify-between gap-3">
        <div>
          <h1 className="text-xl font-semibold">{p.title}</h1>
          <p className="text-sm text-fg-muted">{p.topic}</p>
        </div>
        <div className="flex flex-wrap items-center gap-2">
          {completed && (
            <span className="inline-flex items-center gap-1 rounded-full bg-success-soft px-3 py-1 text-sm font-medium text-success">
              <Icon name="check" size={16} />
              {t("completed")}
            </span>
          )}
          <AskDoubtButton link={{ resourceId: p.resourceId }} about={p.title} />
        </div>
      </div>
      {quiz && quiz.attempts === 0 && <TopicDonePrompt topicId={p.topicId} quiz={quiz} />}
    </div>
  );
}
