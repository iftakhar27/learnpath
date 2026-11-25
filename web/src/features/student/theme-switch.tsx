"use client";

import { useT } from "@/lib/i18n";
import { useTheme, type ThemeChoice } from "@/lib/theme";
import { Icon } from "@/components/ui/icons";

/** Auto (follow the device) / Light / Dark, for Profile. */
export function ThemeSwitch() {
  const t = useT();
  const { choice, setChoice } = useTheme();
  const options: { value: ThemeChoice; label: string }[] = [
    { value: "system", label: t("themeAuto") },
    { value: "light", label: t("themeLight") },
    { value: "dark", label: t("themeDark") },
  ];
  return (
    <div role="group" aria-label={t("appearance")} className="inline-flex items-center gap-1 rounded-lg border border-border bg-surface p-0.5 text-sm">
      {options.map((o) => (
        <button
          key={o.value}
          type="button"
          aria-pressed={choice === o.value}
          onClick={() => setChoice(o.value)}
          className={`rounded-md px-2.5 py-1 font-medium ${choice === o.value ? "bg-primary text-primary-fg" : "text-fg-muted hover:text-fg"}`}
        >
          {o.label}
        </button>
      ))}
    </div>
  );
}

/** One switch for the side menu: on means dark, whatever the device is set to. */
export function NightModeSwitch() {
  const t = useT();
  const { dark, setChoice } = useTheme();
  return (
    <button
      type="button"
      role="switch"
      aria-checked={dark}
      onClick={() => setChoice(dark ? "light" : "dark")}
      className="flex w-full items-center gap-3 rounded-lg px-3 py-2.5 text-left font-medium hover:bg-surface-muted"
    >
      <Icon name="moon" className="text-fg-muted" />
      <span className="flex-1">{t("nightMode")}</span>
      <span aria-hidden className={`relative h-6 w-10 shrink-0 rounded-full transition-colors ${dark ? "bg-primary" : "bg-border"}`}>
        <span className={`absolute top-0.5 size-5 rounded-full bg-surface shadow transition-[left] ${dark ? "left-[1.125rem]" : "left-0.5"}`} />
      </span>
    </button>
  );
}
