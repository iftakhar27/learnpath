import { useSyncExternalStore } from "react";
import { THEME_KEY } from "./theme-boot";

/**
 * Night mode. "system" follows the device; "light" and "dark" are the student's choice, kept in this browser and
 * applied as <html data-theme="…"> (globals.css switches the colour tokens on it). The root layout sets the attribute
 * before the page paints, so a dark choice never flashes white.
 */
export type ThemeChoice = "system" | "light" | "dark";
const BAR_COLOURS = { light: "#ffffff", dark: "#1a1b26" } as const;

function read(): ThemeChoice {
  try {
    const v = localStorage.getItem(THEME_KEY);
    return v === "light" || v === "dark" ? v : "system";
  } catch {
    return "system";
  }
}

let choice: ThemeChoice = typeof window === "undefined" ? "system" : read();
const listeners = new Set<() => void>();

function apply(c: ThemeChoice) {
  const root = document.documentElement;
  if (c === "system") delete root.dataset.theme;
  else root.dataset.theme = c;
  // The phone's address bar follows the page too.
  document.querySelectorAll<HTMLMetaElement>('meta[name="theme-color"]').forEach((m) => {
    m.dataset.original ??= m.content;
    m.content = c === "system" ? m.dataset.original : BAR_COLOURS[c];
  });
}

export const getThemeChoice = () => choice;

export function setThemeChoice(next: ThemeChoice) {
  choice = next;
  try {
    if (next === "system") localStorage.removeItem(THEME_KEY);
    else localStorage.setItem(THEME_KEY, next);
  } catch {
    // Private mode: the choice lasts until the tab closes.
  }
  apply(next);
  listeners.forEach((l) => l());
}

function subscribeChoice(listener: () => void) {
  listeners.add(listener);
  // Another tab changed it.
  const onStorage = (e: StorageEvent) => {
    if (e.key !== THEME_KEY && e.key !== null) return;
    choice = read();
    apply(choice);
    listener();
  };
  window.addEventListener("storage", onStorage);
  return () => {
    listeners.delete(listener);
    window.removeEventListener("storage", onStorage);
  };
}

const deviceDark = () => window.matchMedia("(prefers-color-scheme: dark)");
function subscribeDevice(listener: () => void) {
  const m = deviceDark();
  m.addEventListener("change", listener);
  return () => m.removeEventListener("change", listener);
}

/** The student's choice, whether the page is dark right now, and a setter. */
export function useTheme() {
  const current = useSyncExternalStore(subscribeChoice, getThemeChoice, () => "system" as const);
  const deviceIsDark = useSyncExternalStore(subscribeDevice, () => deviceDark().matches, () => false);
  return { choice: current, dark: current === "dark" || (current === "system" && deviceIsDark), setChoice: setThemeChoice };
}
