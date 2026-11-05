"use client";

import Link from "next/link";
import { useT } from "@/lib/i18n";
import { buttonClass } from "@/components/ui/button";
import { Icon, type IconName } from "@/components/ui/icons";
import { LanguageSwitch } from "./language-switch";

const brand = process.env.NEXT_PUBLIC_BRAND_NAME ?? "LearnPath";

export function Landing() {
  const t = useT();
  const features: { icon: IconName; text: string }[] = [
    { icon: "play", text: t("landingFeatureLearn") },
    { icon: "pencil", text: t("landingFeaturePractice") },
    { icon: "clipboard", text: t("landingFeatureTests") },
  ];
  return (
    <div className="flex min-h-dvh flex-col">
      <header className="mx-auto flex w-full max-w-5xl items-center justify-between gap-3 px-4 py-4">
        <span className="text-lg font-semibold">{brand}</span>
        <div className="flex items-center gap-3">
          <LanguageSwitch compact />
          <Link href="/login" className="text-sm font-medium text-primary hover:underline">
            {t("signIn")}
          </Link>
        </div>
      </header>
      <main className="mx-auto flex w-full max-w-5xl flex-1 flex-col justify-center px-4 py-12">
        <p className="text-sm font-medium text-primary">{t("appTagline")}</p>
        <h1 className="mt-3 max-w-2xl text-4xl font-semibold leading-tight sm:text-5xl">{t("landingTitle")}</h1>
        <p className="mt-4 max-w-xl text-lg text-fg-muted">{t("landingBody")}</p>
        <div className="mt-8 flex flex-wrap gap-3">
          <Link href="/register" className={buttonClass("primary", "md", "h-12 px-6 text-base")}>
            {t("getStarted")}
          </Link>
          <Link href="/login" className={buttonClass("secondary", "md", "h-12 px-6 text-base")}>
            {t("signIn")}
          </Link>
        </div>
        <ul className="mt-12 grid gap-4 sm:grid-cols-3">
          {features.map((f) => (
            <li key={f.icon} className="flex items-start gap-3 rounded-xl border border-border bg-surface p-4">
              <span className="rounded-lg bg-primary-soft p-2 text-primary">
                <Icon name={f.icon} />
              </span>
              <span className="text-sm">{f.text}</span>
            </li>
          ))}
        </ul>
      </main>
      <footer className="mx-auto w-full max-w-5xl px-4 py-6 text-sm text-fg-muted">
        <Link href="/admin" className="hover:underline">
          {t("staffPortal")}
        </Link>
      </footer>
    </div>
  );
}
