"use client";

import Link from "next/link";
import type { ReactNode } from "react";
import { useLanguage } from "@/lib/i18n";

/** Card section with a heading and an optional action link. */
export function Section({ title, action, children, className = "" }: { title: ReactNode; action?: { href: string; label: string }; children: ReactNode; className?: string }) {
  return (
    <section className={`rounded-2xl border border-border bg-surface p-4 sm:p-5 ${className}`}>
      <div className="mb-3 flex items-center justify-between gap-3">
        <h2 className="font-semibold">{title}</h2>
        {action && (
          <Link href={action.href} className="text-sm font-medium text-primary hover:underline">
            {action.label}
          </Link>
        )}
      </div>
      {children}
    </section>
  );
}

export function PageTitle({ children, subtitle, actions }: { children: ReactNode; subtitle?: ReactNode; actions?: ReactNode }) {
  return (
    <div className="mb-6 flex flex-wrap items-end justify-between gap-3">
      <div>
        <h1 className="text-2xl font-semibold">{children}</h1>
        {subtitle && <p className="mt-1 text-fg-muted">{subtitle}</p>}
      </div>
      {actions}
    </div>
  );
}

export function ProgressBar({ value, label }: { value: number; label: string }) {
  const pct = Math.max(0, Math.min(100, Math.round(value)));
  return (
    <div role="progressbar" aria-label={label} aria-valuenow={pct} aria-valuemin={0} aria-valuemax={100} className="h-2 rounded-full bg-surface-muted">
      <div className="h-2 rounded-full bg-primary" style={{ width: `${pct}%` }} />
    </div>
  );
}

export function Muted({ children }: { children: ReactNode }) {
  return <p className="text-sm text-fg-muted">{children}</p>;
}

/** Dates and numbers in the student's language (Indian conventions, IST). */
export function useFormat() {
  const { lang } = useLanguage();
  const locale = lang === "hi" ? "hi-IN" : "en-IN";
  return {
    date: (iso: string) => new Intl.DateTimeFormat(locale, { day: "numeric", month: "short", year: "numeric", timeZone: "Asia/Kolkata" }).format(new Date(iso)),
    dateTime: (iso: string) => new Intl.DateTimeFormat(locale, { day: "numeric", month: "short", hour: "numeric", minute: "2-digit", timeZone: "Asia/Kolkata" }).format(new Date(iso)),
    number: (n: number) => new Intl.NumberFormat(locale).format(n),
    rupees: (paise: number) => new Intl.NumberFormat(locale, { style: "currency", currency: "INR", maximumFractionDigits: paise % 100 === 0 ? 0 : 2 }).format(paise / 100),
    duration: (seconds: number) => {
      const s = Math.max(0, Math.round(seconds));
      const h = Math.floor(s / 3600);
      const m = Math.floor((s % 3600) / 60);
      const sec = String(s % 60).padStart(2, "0");
      return h > 0 ? `${h}:${String(m).padStart(2, "0")}:${sec}` : `${m}:${sec}`;
    },
  };
}
