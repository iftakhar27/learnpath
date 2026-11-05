"use client";

import { useLanguage, useT, type Lang } from "@/lib/i18n";
import { Icon } from "@/components/ui/icons";

/** English / हिंदी toggle. `onChange` defaults to switching the UI only (signed-out pages). */
export function LanguageSwitch({ onChange, compact }: { onChange?: (lang: Lang) => void; compact?: boolean }) {
  const { lang, setLang } = useLanguage();
  const t = useT();
  const change = onChange ?? setLang;
  return (
    <div role="group" aria-label={t("language")} className="inline-flex items-center gap-1 rounded-lg border border-border bg-surface p-0.5 text-sm">
      {!compact && <Icon name="globe" size={16} className="ml-1.5 text-fg-muted" />}
      {(["en", "hi"] as const).map((l) => (
        <button
          key={l}
          type="button"
          lang={l}
          aria-pressed={lang === l}
          onClick={() => change(l)}
          className={`rounded-md px-2.5 py-1 font-medium ${lang === l ? "bg-primary text-primary-fg" : "text-fg-muted hover:text-fg"}`}
        >
          {l === "en" ? t("english") : t("hindi")}
        </button>
      ))}
    </div>
  );
}
