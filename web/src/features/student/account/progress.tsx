"use client";

import Link from "next/link";
import { useEffect, useRef, useState } from "react";
import { useQuery } from "@tanstack/react-query";
import { api, unwrap } from "@/lib/api/client";
import type { components } from "@/lib/api/schema";
import { useT } from "@/lib/i18n";
import { PageLoading } from "@/components/ui/feedback";
import { StudentError } from "../errors";
import { routes } from "../routes";
import { Muted, PageTitle, ProgressBar, Section, useFormat } from "../ui";

type Point = components["schemas"]["ScorePointDto"];

const H = 220;
const PAD = { top: 12, right: 16, bottom: 28, left: 40 };
const TICKS = [0, 25, 50, 75, 100];

/** Score % per test over time: one series, so no legend; hover/focus a point for details; a table view below. */
/** Width of an element in CSS pixels, so the chart draws at 1 unit = 1px and its text stays readable at any size. */
function useWidth<T extends HTMLElement>() {
  const ref = useRef<T>(null);
  const [width, setWidth] = useState(640);
  useEffect(() => {
    const el = ref.current;
    if (!el) return;
    const ro = new ResizeObserver(([entry]) => setWidth(Math.max(280, Math.round(entry.contentRect.width))));
    ro.observe(el);
    return () => ro.disconnect();
  }, []);
  return [ref, width] as const;
}

function ScoreTrend({ points }: { points: Point[] }) {
  const t = useT();
  const f = useFormat();
  const [active, setActive] = useState<number | null>(null);
  const [box, W] = useWidth<HTMLDivElement>();
  const innerW = W - PAD.left - PAD.right;
  const innerH = H - PAD.top - PAD.bottom;
  const x = (i: number) => PAD.left + (points.length === 1 ? innerW / 2 : (i / (points.length - 1)) * innerW);
  const y = (pct: number) => PAD.top + innerH - (Math.max(0, Math.min(100, pct)) / 100) * innerH;
  const path = points.map((p, i) => `${i ? "L" : "M"}${x(i).toFixed(1)},${y(p.percentage).toFixed(1)}`).join(" ");
  const a = active != null ? points[active] : null;

  return (
    <figure className="space-y-3">
      <div ref={box} className="relative">
        <svg viewBox={`0 0 ${W} ${H}`} width={W} height={H} className="block max-w-full" role="img" aria-label={t("scoreTrendSummary", { count: points.length })} onMouseLeave={() => setActive(null)}>
          {TICKS.map((v) => (
            <g key={v}>
              <line x1={PAD.left} x2={W - PAD.right} y1={y(v)} y2={y(v)} stroke="var(--border)" strokeWidth={1} />
              <text x={PAD.left - 8} y={y(v)} textAnchor="end" dominantBaseline="middle" className="fill-[var(--fg-muted)] text-[11px]">
                {v}%
              </text>
            </g>
          ))}
          {points.length > 1 && <path d={path} fill="none" className="stroke-chart-1" strokeWidth={2} strokeLinejoin="round" strokeLinecap="round" />}
          {active != null && <line x1={x(active)} x2={x(active)} y1={PAD.top} y2={PAD.top + innerH} stroke="var(--fg-muted)" strokeWidth={1} opacity={0.4} />}
          {points.map((p, i) => (
            <g key={p.attemptId}>
              <circle cx={x(i)} cy={y(p.percentage)} r={active === i ? 6 : 4} className="fill-chart-1" stroke="var(--surface)" strokeWidth={2} />
              {/* Hit target larger than the mark; focusable for keyboard users. */}
              <circle
                cx={x(i)}
                cy={y(p.percentage)}
                r={14}
                fill="transparent"
                tabIndex={0}
                role="button"
                aria-label={`${p.test}, ${f.date(p.submittedAt)}: ${Math.round(p.percentage)}%`}
                onMouseEnter={() => setActive(i)}
                onFocus={() => setActive(i)}
                onBlur={() => setActive(null)}
                className="cursor-pointer outline-none focus-visible:stroke-[var(--focus)] focus-visible:[stroke-width:2]"
              />
            </g>
          ))}
          {points.length > 0 && (
            <>
              <text x={x(0)} y={H - 8} textAnchor={points.length === 1 ? "middle" : "start"} className="fill-[var(--fg-muted)] text-[11px]">
                {f.date(points[0].submittedAt)}
              </text>
              {points.length > 1 && (
                <text x={x(points.length - 1)} y={H - 8} textAnchor="end" className="fill-[var(--fg-muted)] text-[11px]">
                  {f.date(points[points.length - 1].submittedAt)}
                </text>
              )}
            </>
          )}
        </svg>
        {a && active != null && (
          <div
            role="tooltip"
            className={`pointer-events-none absolute z-10 w-max max-w-56 -translate-x-1/2 rounded-lg border border-border bg-surface px-3 py-2 text-sm shadow-lg ${y(a.percentage) < H / 2 ? "" : "-translate-y-full"}`}
            // Below the point in the upper half (so it stays inside the card), above it otherwise; kept clear of the edges.
            style={{
              left: `${Math.min(Math.max(x(active), 110), W - 110)}px`,
              top: y(a.percentage) < H / 2 ? `${y(a.percentage) + 14}px` : `${y(a.percentage) - 14}px`,
            }}
          >
            <p className="font-medium">{a.test}</p>
            <p className="text-fg-muted">{f.date(a.submittedAt)}</p>
            <p className="tabular-nums">
              {t("scoreOf", { score: f.number(a.score), max: f.number(a.maxScore) })} · {Math.round(a.percentage)}%
            </p>
          </div>
        )}
      </div>
      <details className="text-sm">
        <summary className="cursor-pointer font-medium text-primary">{t("showAsTable")}</summary>
        <table className="mt-2 w-full text-left">
          <thead className="text-fg-muted">
            <tr>
              <th className="py-1 font-medium">{t("test")}</th>
              <th className="py-1 font-medium">{t("date")}</th>
              <th className="py-1 text-right font-medium">{t("score")}</th>
              <th className="py-1 text-right font-medium">{t("percentage")}</th>
            </tr>
          </thead>
          <tbody className="divide-y divide-border">
            {points.map((p) => (
              <tr key={p.attemptId}>
                <td className="py-1.5">
                  <Link href={routes.result(p.attemptId)} className="hover:underline">
                    {p.test}
                  </Link>
                </td>
                <td className="py-1.5">{f.date(p.submittedAt)}</td>
                <td className="py-1.5 text-right tabular-nums">{t("scoreOf", { score: f.number(p.score), max: f.number(p.maxScore) })}</td>
                <td className="py-1.5 text-right tabular-nums">{Math.round(p.percentage)}%</td>
              </tr>
            ))}
          </tbody>
        </table>
      </details>
    </figure>
  );
}

export function ProgressPage() {
  const t = useT();
  const f = useFormat();
  const q = useQuery({ queryKey: ["student", "analytics"], queryFn: () => unwrap(api.GET("/api/v1/student/analytics")) });
  if (q.isPending) return <PageLoading />;
  if (q.isError) return <StudentError error={q.error} onRetry={() => q.refetch()} />;
  const a = q.data;
  const tiles: [string, string][] = [
    [t("testsTaken"), f.number(a.testsTaken)],
    [t("averageScore"), a.averagePercentage != null && a.testsTaken ? `${Math.round(a.averagePercentage)}%` : "—"],
    [t("practiceAccuracy"), a.practiceAccuracy != null && a.practiceQuestionsAnswered ? `${Math.round(a.practiceAccuracy)}%` : "—"],
    [t("lessonsCompletedTotal"), f.number(a.lessonsCompleted)],
  ];
  return (
    <div className="space-y-6">
      <PageTitle subtitle={t("streakDays", { days: a.streakDays })}>{t("progressTitle")}</PageTitle>
      <dl className="grid grid-cols-2 gap-3 lg:grid-cols-4">
        {tiles.map(([label, value]) => (
          <div key={label} className="rounded-2xl border border-border bg-surface p-4">
            <dt className="text-sm text-fg-muted">{label}</dt>
            <dd className="mt-1 text-2xl font-semibold tabular-nums">{value}</dd>
          </div>
        ))}
      </dl>
      <Section title={t("scoreTrend")}>{a.scoreTrend.length === 0 ? <Muted>{t("noTestsYet")}</Muted> : <ScoreTrend points={a.scoreTrend} />}</Section>
      <div className="grid gap-6 md:grid-cols-2">
        <Section title={t("subjectAccuracy")}>
          {a.subjects.length === 0 ? (
            <Muted>{t("emptyGeneric")}</Muted>
          ) : (
            <ul className="space-y-3">
              {a.subjects.map((s) => (
                <li key={s.subjectId}>
                  <div className="flex justify-between text-sm">
                    <span className="font-medium">{s.subject}</span>
                    <span className="tabular-nums text-fg-muted">{t("accuracyOn", { accuracy: Math.round(s.accuracy), count: s.attempted })}</span>
                  </div>
                  <div className="mt-1.5">
                    <ProgressBar value={s.accuracy} label={s.subject} />
                  </div>
                </li>
              ))}
            </ul>
          )}
        </Section>
        <Section title={t("weakTopics")}>
          {a.weakTopics.length === 0 ? (
            <Muted>{t("emptyGeneric")}</Muted>
          ) : (
            <ul className="space-y-3">
              {a.weakTopics.map((w) => (
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
          )}
        </Section>
      </div>
    </div>
  );
}
