"use client";

import Link from "next/link";
import { usePathname, useRouter } from "next/navigation";
import { useEffect, type ReactNode } from "react";
import { useQuery } from "@tanstack/react-query";
import { api, unwrap } from "@/lib/api/client";
import { isLang } from "@/lib/i18n/current";
import { useLanguage, useT, type MessageKey } from "@/lib/i18n";
import { Spinner } from "@/components/ui/feedback";
import { Icon, type IconName } from "@/components/ui/icons";
import { LanguageSwitch } from "./language-switch";
import { StudentError } from "./errors";
import { useChangeLanguage, useProfile } from "./profile";

const brand = process.env.NEXT_PUBLIC_BRAND_NAME ?? "LearnPath";

const NAV: { href: string; label: MessageKey; icon: IconName; also?: string[] }[] = [
  { href: "/home", label: "tabHome", icon: "home", also: ["/plan"] },
  { href: "/learn", label: "tabLearn", icon: "book", also: ["/topics", "/watch"] },
  { href: "/practice", label: "tabPractice", icon: "pencil" },
  { href: "/tests", label: "tabTests", icon: "clipboard", also: ["/exam", "/results"] },
  { href: "/profile", label: "tabProfile", icon: "user", also: ["/progress", "/bookmarks", "/purchases", "/support", "/notifications"] },
];

const isActive = (pathname: string, item: (typeof NAV)[number]) =>
  [item.href, ...(item.also ?? [])].some((p) => pathname === p || pathname.startsWith(p + "/"));

export const notificationsKey = ["student", "notifications"] as const;

/** Frame for signed-in student pages: top bar (desktop nav), bottom tabs on phones, onboarding and language sync. */
export function StudentShell({ children }: { children: ReactNode }) {
  const t = useT();
  const pathname = usePathname();
  const router = useRouter();
  const profile = useProfile();
  const { setLang } = useLanguage();
  const changeLanguage = useChangeLanguage();
  const notifications = useQuery({
    queryKey: notificationsKey,
    queryFn: () => unwrap(api.GET("/api/v1/notifications")),
    enabled: !!profile.data?.onboardingComplete,
  });
  const unread = notifications.data?.items.filter((n) => !n.read).length ?? 0;

  const p = profile.data;
  useEffect(() => {
    if (p && !p.onboardingComplete) router.replace(`/onboarding?next=${encodeURIComponent(pathname)}`);
  }, [p, pathname, router]);
  // The profile's language (also used by the app) wins once the student is set up.
  const profileLang = p?.onboardingComplete ? p.language : null;
  useEffect(() => {
    if (isLang(profileLang)) setLang(profileLang);
  }, [profileLang, setLang]);

  if (profile.isError)
    return (
      <div className="mx-auto max-w-md p-6">
        <StudentError error={profile.error} onRetry={() => profile.refetch()} />
      </div>
    );
  if (!p || !p.onboardingComplete)
    return (
      <div className="flex min-h-dvh items-center justify-center text-fg-muted">
        <Spinner />
      </div>
    );

  return (
    <div className="min-h-dvh pb-[calc(5rem+env(safe-area-inset-bottom))] md:pb-0">
      <a href="#main" className="sr-only focus:not-sr-only focus:fixed focus:left-2 focus:top-2 focus:z-50 focus:rounded-lg focus:bg-surface focus:px-3 focus:py-2">
        {t("skipToContent")}
      </a>
      <header className="sticky top-0 z-30 border-b border-border bg-surface/95 pt-[env(safe-area-inset-top)] backdrop-blur">
        <div className="mx-auto flex h-14 max-w-6xl items-center gap-4 px-4">
          <Link href="/home" className="text-lg font-semibold">
            {brand}
          </Link>
          <nav aria-label={t("primaryNav")} className="hidden md:block">
            <ul className="flex items-center gap-1">
              {NAV.slice(0, 4).map((item) => {
                const on = isActive(pathname, item);
                return (
                  <li key={item.href}>
                    <Link
                      href={item.href}
                      aria-current={on ? "page" : undefined}
                      className={`rounded-lg px-3 py-2 text-sm font-medium ${on ? "bg-primary-soft text-primary" : "text-fg-muted hover:bg-surface-muted hover:text-fg"}`}
                    >
                      {t(item.label)}
                    </Link>
                  </li>
                );
              })}
            </ul>
          </nav>
          <div className="ml-auto flex items-center gap-2">
            <div className="hidden sm:block">
              <LanguageSwitch compact onChange={changeLanguage} />
            </div>
            <Link href="/notifications" className="relative rounded-lg p-2 text-fg-muted hover:bg-surface-muted hover:text-fg" aria-label={`${t("notificationsTitle")}: ${t("unreadCount", { count: unread })}`}>
              <Icon name="bell" />
              {unread > 0 && <span className="absolute right-1 top-1 min-w-4 rounded-full bg-danger px-1 text-center text-[10px] font-semibold leading-4 text-white">{unread > 9 ? "9+" : unread}</span>}
            </Link>
            <Link
              href="/profile"
              aria-current={isActive(pathname, NAV[4]) ? "page" : undefined}
              className="hidden items-center gap-2 rounded-lg px-2 py-1.5 text-sm font-medium text-fg-muted hover:bg-surface-muted hover:text-fg md:flex"
            >
              <span className="flex size-7 items-center justify-center rounded-full bg-primary-soft text-xs font-semibold text-primary" aria-hidden>
                {p.fullName.trim().charAt(0).toUpperCase()}
              </span>
              {t("tabProfile")}
            </Link>
          </div>
        </div>
      </header>
      <main id="main" className="mx-auto max-w-6xl px-4 py-6">
        {children}
      </main>
      <nav aria-label={t("primaryNav")} className="fixed inset-x-0 bottom-0 z-30 border-t border-border bg-surface pb-[env(safe-area-inset-bottom)] md:hidden">
        <ul className="grid grid-cols-5">
          {NAV.map((item) => {
            const on = isActive(pathname, item);
            return (
              <li key={item.href}>
                <Link href={item.href} aria-current={on ? "page" : undefined} className={`flex flex-col items-center gap-0.5 py-2 text-[11px] font-medium ${on ? "text-primary" : "text-fg-muted"}`}>
                  <Icon name={item.icon} size={22} />
                  {t(item.label)}
                </Link>
              </li>
            );
          })}
        </ul>
      </nav>
    </div>
  );
}
