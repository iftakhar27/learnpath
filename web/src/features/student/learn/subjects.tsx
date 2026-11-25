"use client";

import Link from "next/link";
import { useParams } from "next/navigation";
import { useState, type ReactNode } from "react";
import { useQuery } from "@tanstack/react-query";
import { api, unwrap } from "@/lib/api/client";
import type { components } from "@/lib/api/schema";
import { useT } from "@/lib/i18n";
import { buttonClass } from "@/components/ui/button";
import { PageLoading } from "@/components/ui/feedback";
import { Icon } from "@/components/ui/icons";
import { StudentError } from "../errors";
import { routes } from "../routes";
import { ProgressBar } from "../ui";
import { ResourceRow } from "./topic";

type Tile = components["schemas"]["LibraryTileDto"];
type Subject = "Physics" | "Chemistry" | "Biology";

export const libraryKey = ["student", "library"] as const;

/** Colour per subject (tokens in globals.css, light and dark). */
const TONE: Record<Subject, { text: string; soft: string; ring: string }> = {
  Physics: { text: "text-physics", soft: "bg-physics-soft", ring: "stroke-physics" },
  Chemistry: { text: "text-chemistry", soft: "bg-chemistry-soft", ring: "stroke-chemistry" },
  Biology: { text: "text-biology", soft: "bg-biology-soft", ring: "stroke-biology" },
};
const tone = (s: string) => TONE[(s as Subject) in TONE ? (s as Subject) : "Physics"];

/** Our own line icons, one per book: motion, current, flask, benzene ring, leaf, double helix. */
const ICONS: Record<string, ReactNode> = {
  "Physics-11": (
    <>
      <path d="M3 19c3-9 9-13 18-13" strokeDasharray="2.5 2.5" />
      <circle cx="18.5" cy="6.5" r="2.5" fill="currentColor" stroke="none" />
      <path d="M3 21h18" />
    </>
  ),
  "Physics-12": (
    <>
      <circle cx="12" cy="12" r="9" />
      <path d="M13 5.5 8.5 13H12l-1 5.5 4.5-7.5H12z" fill="currentColor" stroke="none" />
    </>
  ),
  "Chemistry-11": (
    <>
      <path d="M9.5 3h5M10.5 3v6L5 18.5A1.6 1.6 0 0 0 6.4 21h11.2a1.6 1.6 0 0 0 1.4-2.5L13.5 9V3" />
      <path d="M7.4 15h9.2" />
      <circle cx="10.5" cy="17.6" r=".9" fill="currentColor" stroke="none" />
      <circle cx="13.6" cy="18.4" r=".7" fill="currentColor" stroke="none" />
    </>
  ),
  "Chemistry-12": (
    <>
      <path d="M12 2.8 20 7.4v9.2L12 21.2 4 16.6V7.4z" />
      <circle cx="12" cy="12" r="4" />
    </>
  ),
  "Biology-11": (
    <>
      <path d="M5 19C5 10 10 5 20 4c0 10-5 15-14 15" />
      <path d="M5 19 13 11" />
    </>
  ),
  "Biology-12": (
    <>
      <path d="M8 3c0 6 8 6 8 9s-8 3-8 9M16 3c0 6-8 6-8 9s8 3 8 9" />
      <path d="M9.2 6.5h5.6M9.2 17.5h5.6" />
    </>
  ),
};

export function SubjectIcon({ subject, classLevel, size = 28 }: { subject: string; classLevel: string; size?: number }) {
  return (
    <svg width={size} height={size} viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth={1.7} strokeLinecap="round" strokeLinejoin="round" aria-hidden>
      {ICONS[`${subject}-${classLevel}`] ?? ICONS["Physics-11"]}
    </svg>
  );
}

/** Icon inside a ring that fills with the student's progress. */
function ProgressRing({ tile, size = 72 }: { tile: Pick<Tile, "subject" | "classLevel" | "progressPercent">; size?: number }) {
  const r = 30;
  const c = 2 * Math.PI * r;
  const t = tone(tile.subject);
  return (
    <span className={`relative inline-flex items-center justify-center rounded-full ${t.soft} ${t.text}`} style={{ width: size, height: size }}>
      <svg viewBox="0 0 72 72" className="absolute inset-0 -rotate-90" width={size} height={size} aria-hidden>
        <circle cx="36" cy="36" r={r} fill="none" strokeWidth="4" className="stroke-border" />
        {tile.progressPercent > 0 && (
          <circle cx="36" cy="36" r={r} fill="none" strokeWidth="4" strokeLinecap="round" className={t.ring} strokeDasharray={`${(c * tile.progressPercent) / 100} ${c}`} />
        )}
      </svg>
      <SubjectIcon subject={tile.subject} classLevel={tile.classLevel} size={size * 0.42} />
    </span>
  );
}

const bookHref = (classLevel: string, subject: string) => `/library/${classLevel}/${subject.toLowerCase()}`;
const chapterHref = (id: string) => `/library/chapter/${id}`;

/** The subject grid: Physics, Chemistry, Biology for Class 11 and 12, with progress and a "continue" shortcut. */
export function SubjectGrid() {
  const t = useT();
  const q = useQuery({ queryKey: libraryKey, queryFn: () => unwrap(api.GET("/api/v1/library")) });
  if (q.isPending) return <div className="h-64 animate-pulse rounded-2xl bg-surface-muted" aria-hidden />;
  if (q.isError) return <StudentError error={q.error} onRetry={() => q.refetch()} />;
  const { tiles, resume } = q.data;

  return (
    <section aria-labelledby="subjects-heading" className="space-y-3">
      <div className="flex flex-wrap items-end justify-between gap-2">
        <div>
          <h2 id="subjects-heading" className="text-lg font-semibold">
            {t("subjectsTitle")}
          </h2>
          <p className="text-sm text-fg-muted">{t("subjectsSubtitle")}</p>
        </div>
        {resume && (
          <Link
            href={chapterHref(resume.chapterId)}
            className={`inline-flex max-w-full items-center gap-1.5 rounded-full px-3 py-1.5 text-sm font-medium ${tone(resume.subject).soft} ${tone(resume.subject).text} hover:opacity-90`}
          >
            <Icon name="play" size={14} />
            <span className="truncate">{t("continueChapter", { title: resume.title })}</span>
          </Link>
        )}
      </div>
      <ul className="grid grid-cols-3 overflow-hidden rounded-2xl border border-border bg-surface">
        {tiles.map((tile, i) => (
          <li key={`${tile.classLevel}-${tile.subject}`} className={`${i % 3 !== 2 ? "border-r" : ""} ${i < 3 ? "border-b" : ""} border-border`}>
            <Link
              href={bookHref(tile.classLevel, tile.subject)}
              aria-label={`${tile.title}, ${t("classLevel", { level: tile.classLevel })}: ${t("chaptersReady", { ready: tile.readyChapters, total: tile.chapters })}`}
              className="group flex h-full flex-col items-center gap-2 px-2 py-4 text-center transition hover:bg-surface-muted active:scale-[0.97] sm:py-5"
            >
              <span className="transition group-hover:scale-105">
                <ProgressRing tile={tile} size={64} />
              </span>
              <span className="text-sm font-semibold leading-tight sm:text-base">
                {tile.title} <span className="text-fg-muted">{tile.classLevel === "11" ? "XI" : "XII"}</span>
              </span>
              <span className="text-xs text-fg-muted">{t("chaptersReady", { ready: tile.readyChapters, total: tile.chapters })}</span>
            </Link>
          </li>
        ))}
      </ul>
    </section>
  );
}

/** Switch between the six books from any book page. */
function BookSwitcher({ classLevel, subject }: { classLevel: string; subject: string }) {
  const t = useT();
  const chip = (active: boolean) => `rounded-full border px-3 py-1 text-sm font-medium ${active ? "border-primary bg-primary-soft text-primary" : "border-border hover:bg-surface-muted"}`;
  return (
    <div className="flex flex-wrap gap-2">
      {(["11", "12"] as const).map((c) => (
        <Link key={c} href={bookHref(c, subject)} className={chip(c === classLevel)} aria-current={c === classLevel ? "page" : undefined}>
          {t("classLevel", { level: c })}
        </Link>
      ))}
      <span className="mx-1 w-px self-stretch bg-border" aria-hidden />
      {(["Physics", "Chemistry", "Biology"] as const).map((s) => (
        <Link key={s} href={bookHref(classLevel, s)} className={chip(s.toLowerCase() === subject.toLowerCase())}>
          {s === "Physics" ? t("physicsShort") : s === "Chemistry" ? t("chemistryShort") : t("biologyShort")}
        </Link>
      ))}
    </div>
  );
}

export function SubjectBookPage() {
  const t = useT();
  const { classLevel, subject } = useParams<{ classLevel: string; subject: string }>();
  const q = useQuery({
    queryKey: [...libraryKey, classLevel, subject],
    queryFn: () => unwrap(api.GET("/api/v1/library/{classLevel}/{subject}", { params: { path: { classLevel, subject } } })),
  });
  if (q.isPending) return <PageLoading />;
  if (q.isError) return <StudentError error={q.error} onRetry={() => q.refetch()} />;
  const book = q.data;
  const tn = tone(book.subject);

  return (
    <div className="mx-auto max-w-3xl space-y-5">
      <Link href={routes.learn} className="inline-flex items-center gap-1 text-sm font-medium text-primary hover:underline">
        <Icon name="chevronLeft" size={16} />
        {t("tabLearn")}
      </Link>
      <header className="flex items-center gap-4">
        <ProgressRing tile={{ subject: book.subject, classLevel: book.classLevel, progressPercent: book.progressPercent }} size={72} />
        <div className="min-w-0">
          <h1 className="text-2xl font-semibold">
            {book.title} · {t("classLevel", { level: book.classLevel })}
          </h1>
          <p className="text-sm text-fg-muted">
            NCERT · {t("chaptersReady", { ready: book.chapters.filter((c) => c.ready).length, total: book.chapters.length })} ·{" "}
            {t("percentComplete", { percent: book.progressPercent })}
          </p>
        </div>
      </header>
      <BookSwitcher classLevel={book.classLevel} subject={book.subject} />

      <ol className="space-y-2">
        {book.chapters.map((c) => (
          <li key={c.id}>
            <Link
              href={chapterHref(c.id)}
              className={`flex items-center gap-3 rounded-xl border border-border bg-surface p-3 hover:border-primary ${c.ready ? "" : "opacity-75"}`}
            >
              <span className={`flex size-10 shrink-0 items-center justify-center rounded-lg text-sm font-semibold tabular-nums ${tn.soft} ${tn.text}`}>
                {String(c.number).padStart(2, "0")}
              </span>
              <span className="min-w-0 flex-1">
                <span className="block font-medium">{c.title}</span>
                <span className="mt-0.5 flex flex-wrap items-center gap-x-2 text-xs text-fg-muted">
                  {c.ready ? (
                    <>
                      <span>{t("topicsCount", { count: c.topics })}</span>
                      {c.videos > 0 && <span>· {t("videosCount", { count: c.videos })}</span>}
                      {c.notes > 0 && <span>· {t("notesCount", { count: c.notes })}</span>}
                    </>
                  ) : (
                    <span>{t("chapterComingSoon")}</span>
                  )}
                </span>
                {c.progressPercent > 0 && (
                  <span className="mt-2 block max-w-xs">
                    <ProgressBar value={c.progressPercent} label={t("percentComplete", { percent: c.progressPercent })} />
                  </span>
                )}
              </span>
              {c.locked ? <Icon name="lock" size={18} className="text-fg-muted" aria-label={t("locked")} role="img" /> : <Icon name="chevronRight" className="text-fg-muted" />}
            </Link>
          </li>
        ))}
      </ol>
    </div>
  );
}

type Tab = "videos" | "notes" | "practice";

export function NcertChapterPage() {
  const t = useT();
  const { chapterId } = useParams<{ chapterId: string }>();
  const q = useQuery({
    queryKey: [...libraryKey, "chapter", chapterId],
    queryFn: () => unwrap(api.GET("/api/v1/library/chapters/{id}", { params: { path: { id: chapterId } } })),
  });
  const [tab, setTab] = useState<Tab>("videos");
  const [error, setError] = useState<unknown>(null);
  if (q.isPending) return <PageLoading />;
  if (q.isError) return <StudentError error={q.error} onRetry={() => q.refetch()} />;
  const ch = q.data;
  const tn = tone(ch.subject);
  const kind = tab === "videos" ? "Video" : "Document";
  const groups = ch.topics.map((tp) => ({ ...tp, items: tp.resources.filter((r) => r.type === kind) })).filter((g) => g.items.length > 0);
  const tabs: { id: Tab; label: string }[] = [
    { id: "videos", label: t("videos") },
    { id: "notes", label: t("notes") },
    { id: "practice", label: t("tabPractice") },
  ];

  return (
    <div className="mx-auto max-w-3xl space-y-5">
      <Link href={bookHref(ch.classLevel, ch.subject)} className="inline-flex items-center gap-1 text-sm font-medium text-primary hover:underline">
        <Icon name="chevronLeft" size={16} />
        {ch.subjectTitle} · {t("classLevel", { level: ch.classLevel })}
      </Link>
      <header className="flex items-center gap-3">
        <span className={`flex size-12 shrink-0 items-center justify-center rounded-xl ${tn.soft} ${tn.text}`}>
          <SubjectIcon subject={ch.subject} classLevel={ch.classLevel} size={26} />
        </span>
        <div className="min-w-0">
          <p className="text-sm text-fg-muted">{t("chapterNumber", { number: ch.number })}</p>
          <h1 className="text-2xl font-semibold">{ch.title}</h1>
        </div>
      </header>

      {ch.buyCourseId && (
        <p className="flex flex-wrap items-center justify-between gap-3 rounded-xl bg-warning-soft px-4 py-3 text-sm text-warning">
          {t("chapterLocked")}
          <Link href={routes.course(ch.buyCourseId)} className="font-medium underline">
            {t("viewCourse")}
          </Link>
        </p>
      )}

      {ch.topics.length === 0 ? (
        <p className="rounded-2xl border border-dashed border-border p-6 text-center text-fg-muted">{t("chapterComingSoon")}</p>
      ) : (
        <>
          <div role="tablist" aria-label={ch.title} className="grid grid-cols-3 gap-1 rounded-xl bg-surface-muted p-1">
            {tabs.map((x) => (
              <button
                key={x.id}
                type="button"
                role="tab"
                aria-selected={tab === x.id}
                onClick={() => setTab(x.id)}
                className={`rounded-lg px-3 py-2 text-sm font-medium ${tab === x.id ? "bg-surface shadow-sm" : "text-fg-muted hover:text-fg"}`}
              >
                {x.label}
              </button>
            ))}
          </div>
          <StudentError error={error} />
          {tab === "practice" ? (
            <section role="tabpanel" className="rounded-2xl border border-border bg-surface p-5">
              {ch.canPractise && ch.practiceQuestionCount > 0 ? (
                <Link href={routes.practiceChapter(ch.id)} className={buttonClass("primary", "md", "w-full sm:w-auto")}>
                  <Icon name="pencil" size={18} />
                  {t("practiseChapter", { count: ch.practiceQuestionCount })}
                </Link>
              ) : (
                <p className="text-sm text-fg-muted">{ch.canPractise ? t("noPracticeQuestions") : t("chapterLocked")}</p>
              )}
            </section>
          ) : groups.length === 0 ? (
            <p role="tabpanel" className="rounded-2xl border border-dashed border-border p-6 text-center text-fg-muted">
              {tab === "videos" ? t("noVideosYet") : t("noNotesYet")}
            </p>
          ) : (
            <div role="tabpanel" className="space-y-5">
              {groups.map((g) => (
                <section key={g.id} className="space-y-2">
                  <h2 className="flex flex-wrap items-baseline justify-between gap-2">
                    <Link href={routes.topic(g.id)} className="font-semibold hover:underline">
                      {g.title}
                    </Link>
                    <span className="text-xs text-fg-muted">{t("fromCourse", { course: g.course })}</span>
                  </h2>
                  {g.items.map((r) => (
                    <ResourceRow key={r.id} r={r} onError={setError} />
                  ))}
                </section>
              ))}
            </div>
          )}
        </>
      )}

      <nav className="flex justify-between gap-3" aria-label={t("syllabus")}>
        {ch.previousChapterId ? (
          <Link href={chapterHref(ch.previousChapterId)} className={buttonClass("secondary")}>
            <Icon name="chevronLeft" size={16} />
            {t("previous")}
          </Link>
        ) : (
          <span />
        )}
        {ch.nextChapterId && (
          <Link href={chapterHref(ch.nextChapterId)} className={buttonClass("secondary")}>
            {t("next")}
            <Icon name="chevronRight" size={16} />
          </Link>
        )}
      </nav>
    </div>
  );
}
