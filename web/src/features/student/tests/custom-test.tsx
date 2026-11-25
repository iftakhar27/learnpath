"use client";

import Link from "next/link";
import { useRouter } from "next/navigation";
import { useMemo, useState } from "react";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { api, unwrap } from "@/lib/api/client";
import type { components } from "@/lib/api/schema";
import { useT, type MessageKey } from "@/lib/i18n";
import { Button } from "@/components/ui/button";
import { PageLoading } from "@/components/ui/feedback";
import { Icon } from "@/components/ui/icons";
import { StudentError } from "../errors";
import { tone } from "../learn/subjects";
import { routes } from "../routes";
import { PageTitle } from "../ui";

type Chapter = components["schemas"]["CustomTestChapterDto"];
type Difficulty = "Easy" | "Medium" | "Hard";

export const customTestsKey = ["student", "tests", "custom"] as const;
const COUNTS = [10, 20, 30, 45, 90, 180];
const DIFFICULTIES: { value: Difficulty | null; label: MessageKey }[] = [
  { value: null, label: "difficultyAny" },
  { value: "Easy", label: "difficultyEasy" },
  { value: "Medium", label: "difficultyMedium" },
  { value: "Hard", label: "difficultyHard" },
];

const usable = (c: Chapter, d: Difficulty | null) => (d === "Easy" ? c.easy : d === "Medium" ? c.medium : d === "Hard" ? c.hard : c.easy + c.medium + c.hard);

/** Pick chapters, number of questions and difficulty; creating it opens the test's instructions, ready to start. */
export function CustomTestGenerator() {
  const t = useT();
  const router = useRouter();
  const qc = useQueryClient();
  const options = useQuery({ queryKey: [...customTestsKey, "options"], queryFn: () => unwrap(api.GET("/api/v1/tests/custom/options")) });
  const [subject, setSubject] = useState<string | null>(null);
  const [selected, setSelected] = useState<Set<string>>(new Set());
  const [count, setCount] = useState(30);
  const [difficulty, setDifficulty] = useState<Difficulty | null>(null);
  const [skipCorrect, setSkipCorrect] = useState(true);

  const all = useMemo(() => options.data?.subjects.flatMap((s) => s.chapters) ?? [], [options.data]);
  const available = all.filter((c) => selected.has(c.id)).reduce((sum, c) => sum + usable(c, difficulty), 0);
  const max = Math.min(available, options.data?.maxQuestions ?? 180);
  const questions = Math.min(count, max);
  const minutes = Math.max(5, questions * (options.data?.minutesPerQuestion ?? 1));

  const create = useMutation({
    mutationFn: () =>
      unwrap(api.POST("/api/v1/tests/custom", { body: { chapterIds: [...selected], count: questions, difficulty: difficulty ?? undefined, skipCorrect } })),
    onSuccess: (test) => {
      void qc.invalidateQueries({ queryKey: customTestsKey });
      router.push(routes.test(test.id));
    },
  });

  if (options.isPending) return <PageLoading />;
  if (options.isError) return <StudentError error={options.error} onRetry={() => options.refetch()} />;
  const subjects = options.data.subjects;
  const current = subjects.find((s) => s.subject === subject) ?? subjects[0];
  const toggle = (ids: string[], on: boolean) =>
    setSelected((prev) => {
      const next = new Set(prev);
      ids.forEach((id) => (on ? next.add(id) : next.delete(id)));
      return next;
    });

  return (
    <div className="mx-auto max-w-3xl pb-28">
      <Link href={routes.tests} className="mb-3 inline-flex items-center gap-1 text-sm font-medium text-primary hover:underline">
        <Icon name="chevronLeft" size={16} />
        {t("testsTitle")}
      </Link>
      <PageTitle subtitle={t("customTestIntro")}>{t("customTestTitle")}</PageTitle>

      {subjects.length === 0 ? (
        <p className="rounded-2xl border border-dashed border-border p-6 text-center text-fg-muted">{t("customTestNoChapters")}</p>
      ) : (
        <div className="space-y-5">
          <section aria-labelledby="ct-chapters" className="rounded-2xl border border-border bg-surface p-4 sm:p-5">
            <div className="mb-3 flex flex-wrap items-center justify-between gap-2">
              <h2 id="ct-chapters" className="font-semibold">
                {t("chooseChapters")}
              </h2>
              <span className="text-sm text-fg-muted">{t("chaptersSelected", { count: selected.size })}</span>
            </div>
            <div role="tablist" aria-label={t("chooseChapters")} className="mb-4 flex gap-1 overflow-x-auto rounded-xl bg-surface-muted p-1">
              {subjects.map((s) => {
                const picked = s.chapters.filter((c) => selected.has(c.id)).length;
                return (
                  <button
                    key={s.subject}
                    type="button"
                    role="tab"
                    aria-selected={current.subject === s.subject}
                    onClick={() => setSubject(s.subject)}
                    className={`flex-1 whitespace-nowrap rounded-lg px-3 py-2 text-sm font-medium ${current.subject === s.subject ? "bg-surface shadow-sm" : "text-fg-muted hover:text-fg"}`}
                  >
                    {s.title}
                    {picked > 0 && <span className={`ml-1.5 rounded-full px-1.5 text-xs ${tone(s.subject).soft} ${tone(s.subject).text}`}>{picked}</span>}
                  </button>
                );
              })}
            </div>
            <div role="tabpanel" className="space-y-4">
              {["11", "12"].map((cls) => {
                const chapters = current.chapters.filter((c) => c.classLevel === cls);
                if (chapters.length === 0) return null;
                const open = chapters.filter((c) => usable(c, null) > 0).map((c) => c.id);
                const allOn = open.length > 0 && open.every((id) => selected.has(id));
                return (
                  <fieldset key={cls}>
                    <legend className="flex w-full items-center justify-between gap-2 pb-1">
                      <span className="text-sm font-semibold text-fg-muted">
                        {current.title} · {t("classLevel", { level: cls })}
                      </span>
                    </legend>
                    {open.length > 1 && (
                      <button type="button" onClick={() => toggle(open, !allOn)} className="mb-1 text-sm font-medium text-primary hover:underline">
                        {allOn ? t("clearSelection") : t("selectAll")}
                      </button>
                    )}
                    <ul className="divide-y divide-border rounded-xl border border-border">
                      {chapters.map((c) => {
                        const n = usable(c, difficulty);
                        const disabled = usable(c, null) === 0;
                        return (
                          <li key={c.id}>
                            <label className={`flex items-start gap-3 px-3 py-2.5 ${disabled ? "opacity-60" : "cursor-pointer hover:bg-surface-muted"}`}>
                              <input
                                type="checkbox"
                                className="mt-1 size-4 accent-[var(--primary)]"
                                checked={selected.has(c.id)}
                                disabled={disabled}
                                onChange={(e) => toggle([c.id], e.target.checked)}
                              />
                              <span className="min-w-0 flex-1">
                                <span className="block font-medium">
                                  {c.number}. {c.title}
                                </span>
                                <span className="block text-xs text-fg-muted">
                                  {disabled ? t("onlyInPaidCourses") : t("questionsAvailable", { count: n })}
                                  {!disabled && c.locked > 0 && <> · {t("moreInPaidCourses", { count: c.locked })}</>}
                                </span>
                              </span>
                            </label>
                          </li>
                        );
                      })}
                    </ul>
                  </fieldset>
                );
              })}
            </div>
          </section>

          <section aria-labelledby="ct-count" className="rounded-2xl border border-border bg-surface p-4 sm:p-5">
            <h2 id="ct-count" className="mb-1 font-semibold">
              {t("numberOfQuestions")}
            </h2>
            <p className="mb-3 text-sm text-fg-muted">{t("questionsAvailable", { count: available })}</p>
            <div role="group" aria-labelledby="ct-count" className="flex flex-wrap gap-2">
              {[...COUNTS.filter((n) => n < max), ...(max > 0 ? [max] : [])].map((n) => (
                <button
                  key={n}
                  type="button"
                  aria-pressed={questions === n}
                  onClick={() => setCount(n)}
                  className={`rounded-full border px-3.5 py-1.5 text-sm font-medium ${questions === n ? "border-primary bg-primary text-primary-fg" : "border-border hover:border-primary"}`}
                >
                  {n === max && !COUNTS.includes(n) ? t("allQuestionsCount", { count: n }) : n}
                </button>
              ))}
            </div>
          </section>

          <section aria-labelledby="ct-difficulty" className="rounded-2xl border border-border bg-surface p-4 sm:p-5">
            <h2 id="ct-difficulty" className="mb-3 font-semibold">
              {t("difficulty")}
            </h2>
            <div role="group" aria-labelledby="ct-difficulty" className="inline-flex flex-wrap gap-1 rounded-xl bg-surface-muted p-1">
              {DIFFICULTIES.map((d) => (
                <button
                  key={d.label}
                  type="button"
                  aria-pressed={difficulty === d.value}
                  onClick={() => setDifficulty(d.value)}
                  className={`rounded-lg px-3 py-1.5 text-sm font-medium ${difficulty === d.value ? "bg-surface shadow-sm" : "text-fg-muted hover:text-fg"}`}
                >
                  {t(d.label)}
                </button>
              ))}
            </div>
            <label className="mt-4 flex cursor-pointer items-start gap-3">
              <input type="checkbox" className="mt-1 size-4 accent-[var(--primary)]" checked={skipCorrect} onChange={(e) => setSkipCorrect(e.target.checked)} />
              <span>
                <span className="block font-medium">{t("skipCorrect")}</span>
                <span className="block text-sm text-fg-muted">{t("skipCorrectHint")}</span>
              </span>
            </label>
          </section>
          <StudentError error={create.error} />
        </div>
      )}

      {subjects.length > 0 && (
        <div className="fixed inset-x-0 bottom-[calc(4.25rem+env(safe-area-inset-bottom))] z-20 border-t border-border bg-surface/95 backdrop-blur md:bottom-0">
          <div className="mx-auto flex max-w-3xl items-center justify-between gap-3 px-4 py-3">
            <p className="text-sm" aria-live="polite">
              {selected.size === 0 ? (
                <span className="text-fg-muted">{t("customTestPickChapter")}</span>
              ) : (
                <>
                  <span className="font-semibold">{t("questionsCount", { count: questions })}</span>
                  <span className="text-fg-muted"> · {t("minutesCount", { minutes })}</span>
                </>
              )}
            </p>
            <Button onClick={() => create.mutate()} loading={create.isPending} disabled={selected.size === 0 || questions === 0}>
              {t("createTest")}
            </Button>
          </div>
        </div>
      )}
    </div>
  );
}
