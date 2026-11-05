/** The UI language as a tiny external store, readable outside React (the API client sends it as Accept-Language). */
export type Lang = "en" | "hi";
export const LANG_COOKIE = "lp_lang";
export const isLang = (v: unknown): v is Lang => v === "en" || v === "hi";

function readCookie(): Lang | null {
  if (typeof document === "undefined") return null;
  const match = document.cookie.match(new RegExp(`(?:^|; )${LANG_COOKIE}=(en|hi)`));
  return match && isLang(match[1]) ? match[1] : null;
}

let current: Lang = readCookie() ?? "en";
if (typeof document !== "undefined") document.documentElement.lang = current;
const listeners = new Set<() => void>();

export const getLanguage = () => current;
/** The server can't know the visitor's choice, so it always renders English; the browser then switches. */
export const getServerLanguage = (): Lang => "en";

export function subscribeLanguage(listener: () => void) {
  listeners.add(listener);
  return () => {
    listeners.delete(listener);
  };
}

export function setCurrentLanguage(lang: Lang) {
  if (lang === current) return;
  current = lang;
  document.documentElement.lang = lang;
  document.cookie = `${LANG_COOKIE}=${lang}; path=/; max-age=31536000; samesite=lax`;
  listeners.forEach((l) => l());
}
