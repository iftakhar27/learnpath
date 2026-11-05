"use client";

import { IntlMessageFormat } from "intl-messageformat";
import { useQueryClient } from "@tanstack/react-query";
import { useCallback, useSyncExternalStore } from "react";
import { getLanguage, getServerLanguage, setCurrentLanguage, subscribeLanguage, type Lang } from "./current";
import { appMessages } from "./messages.gen";
import { webMessages } from "./web-messages";

export type { Lang } from "./current";

const messages = {
  en: { ...appMessages.en, ...webMessages.en },
  hi: { ...appMessages.hi, ...webMessages.hi },
};
export type MessageKey = keyof typeof messages.en;
type Values = Record<string, string | number>;

const formatters = new Map<string, IntlMessageFormat>();

/** Formats one message (ICU syntax, as in the app's ARB files) in the given language. */
export function translate(lang: Lang, key: MessageKey, values?: Values): string {
  const id = `${lang}:${key}`;
  let f = formatters.get(id);
  if (!f) {
    f = new IntlMessageFormat(messages[lang][key], lang === "hi" ? "hi-IN" : "en-IN");
    formatters.set(id, f);
  }
  return String(f.format(values));
}

/** The current UI language and a setter; changing it refetches API content in the new language. */
export function useLanguage() {
  const qc = useQueryClient();
  const lang = useSyncExternalStore(subscribeLanguage, getLanguage, getServerLanguage);
  const setLang = useCallback(
    (next: Lang) => {
      if (next === getLanguage()) return;
      setCurrentLanguage(next);
      // Content (course titles, explanations) comes from the API in the chosen language.
      void qc.invalidateQueries({ predicate: (q) => q.queryKey[0] !== "session" });
    },
    [qc],
  );
  return { lang, setLang };
}

/** `t("key", { values })` in the current language. */
export function useT() {
  const { lang } = useLanguage();
  return useCallback((key: MessageKey, values?: Values) => translate(lang, key, values), [lang]);
}
