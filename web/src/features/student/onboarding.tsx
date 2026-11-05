"use client";

import Link from "next/link";
import { useRouter, useSearchParams } from "next/navigation";
import { useEffect, useState } from "react";
import { useQuery } from "@tanstack/react-query";
import { api, unwrap } from "@/lib/api/client";
import { useLanguage, useT } from "@/lib/i18n";
import { Button } from "@/components/ui/button";
import { Spinner } from "@/components/ui/feedback";
import { Field, Input } from "@/components/ui/field";
import { useToast } from "@/components/ui/toast";
import { LanguageSwitch } from "./language-switch";
import { StudentError } from "./errors";
import { safeNext, useProfile, useUpdateProfile, type Profile } from "./profile";

const CLASSES = ["9", "10", "11", "12", "Dropper"];
const BOARDS = ["CBSE", "ICSE", "State"];
const brand = process.env.NEXT_PUBLIC_BRAND_NAME ?? "LearnPath";

function Chip({ selected, onClick, children }: { selected: boolean; onClick: () => void; children: React.ReactNode }) {
  return (
    <button
      type="button"
      role="radio"
      aria-checked={selected}
      onClick={onClick}
      className={`rounded-full border px-4 py-2 text-sm font-medium ${selected ? "border-primary bg-primary-soft text-primary" : "border-border bg-surface hover:bg-surface-muted"}`}
    >
      {children}
    </button>
  );
}

/** First-run setup (also used for "Study preferences" with `editing`). Mirrors the app's three steps. */
export function Onboarding({ editing = false }: { editing?: boolean }) {
  const profile = useProfile();
  if (profile.isPending)
    return (
      <div className="flex min-h-dvh items-center justify-center text-fg-muted">
        <Spinner />
      </div>
    );
  if (profile.isError) return <StudentError error={profile.error} onRetry={() => profile.refetch()} />;
  return <OnboardingSteps profile={profile.data} editing={editing} />;
}

function OnboardingSteps({ profile, editing }: { profile: Profile; editing: boolean }) {
  const t = useT();
  const { lang } = useLanguage();
  const router = useRouter();
  const params = useSearchParams();
  const exams = useQuery({ queryKey: ["exams"], queryFn: () => unwrap(api.GET("/api/v1/exams")) });
  const save = useUpdateProfile();
  const toast = useToast();
  const thisYear = new Date().getFullYear();

  const [step, setStep] = useState(0);
  const [examId, setExamId] = useState<string | null>(profile.targetExamId ?? null);
  const [year, setYear] = useState(profile.targetYear ?? thisYear + 1);
  const [classLevel, setClassLevel] = useState<string | null>(profile.classLevel ?? null);
  const [board, setBoard] = useState<string | null>(profile.board ?? null);
  const [minutes, setMinutes] = useState(Math.min(480, Math.max(30, profile.dailyStudyMinutes || 120)));
  const [minor, setMinor] = useState(profile.isMinor);
  const [guardian, setGuardian] = useState(profile.guardianName ?? "");
  const [consent, setConsent] = useState(false);
  const [error, setError] = useState<string | null>(null);

  // Already set up (e.g. opened /onboarding by hand): go home.
  useEffect(() => {
    if (!editing && profile.onboardingComplete) router.replace(safeNext(params.get("next")));
  }, [editing, profile.onboardingComplete, router, params]);

  const finish = async () => {
    if (!examId) return setError(t("selectExamError"));
    if (minor && (!guardian.trim() || !consent)) return setError(t("guardianConsent"));
    setError(null);
    await save.mutateAsync({
      targetExamId: examId,
      targetYear: year,
      classLevel,
      board,
      dailyStudyMinutes: minutes,
      language: lang,
      isMinor: minor,
      guardianName: minor ? guardian.trim() : null,
      guardianConsent: minor && consent ? true : null,
      completeOnboarding: true,
    });
    if (editing) toast(t("savedToast"));
    router.replace(editing ? "/profile" : safeNext(params.get("next")));
  };

  const steps = [
    <div key="exam" className="space-y-6">
      <section>
        <h2 className="text-lg font-semibold">{t("chooseExam")}</h2>
        {exams.isPending ? (
          <div className="mt-4 text-fg-muted">
            <Spinner />
          </div>
        ) : exams.isError ? (
          <div className="mt-4">
            <StudentError error={exams.error} onRetry={() => exams.refetch()} />
          </div>
        ) : (
          <div role="radiogroup" aria-label={t("chooseExam")} className="mt-4 grid gap-2 sm:grid-cols-2">
            {exams.data.map((e) => (
              <button
                key={e.id}
                type="button"
                role="radio"
                aria-checked={examId === e.id}
                onClick={() => setExamId(e.id)}
                className={`rounded-xl border p-4 text-left font-medium ${examId === e.id ? "border-primary bg-primary-soft text-primary" : "border-border bg-surface hover:bg-surface-muted"}`}
              >
                {e.name}
              </button>
            ))}
          </div>
        )}
      </section>
      <section>
        <h2 className="font-semibold">{t("targetYear")}</h2>
        <div role="radiogroup" aria-label={t("targetYear")} className="mt-3 flex flex-wrap gap-2">
          {[0, 1, 2, 3].map((i) => (
            <Chip key={i} selected={year === thisYear + i} onClick={() => setYear(thisYear + i)}>
              {thisYear + i}
            </Chip>
          ))}
        </div>
      </section>
    </div>,
    <div key="class" className="space-y-6">
      <section>
        <h2 className="text-lg font-semibold">{t("yourClass")}</h2>
        <div role="radiogroup" aria-label={t("yourClass")} className="mt-4 flex flex-wrap gap-2">
          {CLASSES.map((c) => (
            <Chip key={c} selected={classLevel === c} onClick={() => setClassLevel(c)}>
              {c === "Dropper" ? t("classDropper") : t("classLabel", { level: c })}
            </Chip>
          ))}
        </div>
      </section>
      <section>
        <h2 className="text-lg font-semibold">{t("board")}</h2>
        <div role="radiogroup" aria-label={t("board")} className="mt-4 flex flex-wrap gap-2">
          {BOARDS.map((b) => (
            <Chip key={b} selected={board === b} onClick={() => setBoard(b)}>
              {b === "State" ? t("stateBoard") : b}
            </Chip>
          ))}
        </div>
      </section>
    </div>,
    <div key="time" className="space-y-6">
      <section>
        <h2 className="text-lg font-semibold">{t("dailyStudyTime")}</h2>
        <label className="mt-4 block">
          <span className="sr-only">{t("dailyStudyTime")}</span>
          <input type="range" min={30} max={480} step={30} value={minutes} onChange={(e) => setMinutes(Number(e.target.value))} className="w-full accent-[var(--primary)]" />
        </label>
        <p className="mt-2 text-center font-medium">{t("minutesPerDay", { minutes })}</p>
      </section>
      <section>
        <h2 className="font-semibold">{t("language")}</h2>
        <div className="mt-3">
          <LanguageSwitch />
        </div>
      </section>
      <section className="space-y-3">
        <label className="flex items-center gap-2 text-sm">
          <input type="checkbox" className="size-4 accent-[var(--primary)]" checked={minor} onChange={(e) => setMinor(e.target.checked)} />
          {t("under18")}
        </label>
        {minor && (
          <div className="space-y-3 rounded-xl border border-border p-4">
            <Field label={t("guardianName")}>
              <Input value={guardian} onChange={(e) => setGuardian(e.target.value)} autoComplete="off" />
            </Field>
            <label className="flex items-start gap-2 text-sm">
              <input type="checkbox" className="mt-0.5 size-4 accent-[var(--primary)]" checked={consent} onChange={(e) => setConsent(e.target.checked)} />
              {t("guardianConsent")}
            </label>
          </div>
        )}
      </section>
    </div>,
  ];

  const body = (
    <>
      <h1 className="text-2xl font-semibold">{editing ? t("editPreferences") : t("onboardingTitle")}</h1>
      <div className="mt-2 h-1.5 rounded-full bg-surface-muted" aria-hidden>
        <div className="h-1.5 rounded-full bg-primary transition-all" style={{ width: `${((step + 1) / steps.length) * 100}%` }} />
      </div>
      <div className="mt-6">{steps[step]}</div>
      <div className="mt-6 space-y-3">
        {error && (
          <p role="alert" className="rounded-lg bg-danger-soft px-3 py-2 text-sm text-danger">
            {error}
          </p>
        )}
        <StudentError error={save.error} />
        <div className="flex justify-between gap-3">
          <Button variant="secondary" onClick={() => setStep((s) => s - 1)} disabled={step === 0}>
            {t("back")}
          </Button>
          {step < steps.length - 1 ? (
            <Button
              onClick={() => {
                if (step === 0 && !examId) return setError(t("selectExamError"));
                setError(null);
                setStep((s) => s + 1);
              }}
            >
              {t("next")}
            </Button>
          ) : (
            <Button onClick={finish} loading={save.isPending}>
              {editing ? t("save") : t("getStarted")}
            </Button>
          )}
        </div>
      </div>
    </>
  );

  // Editing from the profile happens inside the site frame.
  if (editing)
    return (
      <div className="mx-auto max-w-2xl">
        <div className="mb-4 flex items-center justify-between">
          <Link href="/profile" className="text-sm font-medium text-primary hover:underline">
            ← {t("profileTitle")}
          </Link>
          <span className="text-sm text-fg-muted">{t("stepOf", { current: step + 1, total: steps.length })}</span>
        </div>
        {body}
      </div>
    );

  return (
    <div className="flex min-h-dvh flex-col">
      <header className="mx-auto flex w-full max-w-2xl items-center justify-between px-4 py-4">
        <span className="text-lg font-semibold">{brand}</span>
        <span className="text-sm text-fg-muted">{t("stepOf", { current: step + 1, total: steps.length })}</span>
      </header>
      <main className="mx-auto w-full max-w-2xl flex-1 px-4 pb-8">{body}</main>
    </div>
  );
}

export function PreferencesPage() {
  return <Onboarding editing />;
}
