"use client";

import Link from "next/link";
import type { ReactNode } from "react";
import { useWakeApi } from "@/lib/api/warm-up";
import { LanguageSwitch } from "./language-switch";

const brand = process.env.NEXT_PUBLIC_BRAND_NAME ?? "LearnPath";

/** Signed-out pages: brand, language choice, and a centred card. */
export function AuthLayout({ title, subtitle, children, footer }: { title: string; subtitle?: string; children: ReactNode; footer?: ReactNode }) {
  useWakeApi();
  return (
    <div className="flex min-h-dvh flex-col">
      <header className="mx-auto flex w-full max-w-5xl items-center justify-between px-4 py-4">
        <Link href="/" className="text-lg font-semibold">
          {brand}
        </Link>
        <LanguageSwitch />
      </header>
      <main className="flex flex-1 items-start justify-center px-4 pb-12 pt-4 sm:items-center">
        <div className="w-full max-w-md">
          <div className="rounded-2xl border border-border bg-surface p-6 shadow-sm sm:p-8">
            <h1 className="text-2xl font-semibold">{title}</h1>
            {subtitle && <p className="mt-1 text-sm text-fg-muted">{subtitle}</p>}
            <div className="mt-6">{children}</div>
          </div>
          {footer && <div className="mt-4 text-center text-sm">{footer}</div>}
        </div>
      </main>
    </div>
  );
}
