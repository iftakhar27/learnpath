"use client";

import Link from "next/link";
import { usePathname } from "next/navigation";
import { useEffect, useLayoutEffect, useRef, type ReactNode } from "react";
import { useQuery } from "@tanstack/react-query";
import { api, unwrap } from "@/lib/api/client";
import { useT, type MessageKey } from "@/lib/i18n";
import type { components } from "@/lib/api/schema";
import { Icon, type IconName } from "@/components/ui/icons";
import { signOut } from "./account/profile";
import { LanguageSwitch } from "./language-switch";
import { useChangeLanguage } from "./profile";
import { NightModeSwitch } from "./theme-switch";

type Item = { href: string; label: MessageKey; icon: IconName };
const STUDY: Item[] = [
  { href: "/home", label: "tabHome", icon: "home" },
  { href: "/learn", label: "menuSubjectsCourses", icon: "book" },
  { href: "/practice", label: "tabPractice", icon: "pencil" },
  { href: "/tests", label: "testsTitle", icon: "clipboard" },
  { href: "/plan", label: "planTitle", icon: "clock" },
  { href: "/progress", label: "myProgress", icon: "check" },
  { href: "/bookmarks", label: "bookmarksTitle", icon: "bookmark" },
];
const HELP: Item[] = [
  { href: "/doubts", label: "askDoubt", icon: "chat" },
  { href: "/notifications", label: "notificationsTitle", icon: "bell" },
  { href: "/purchases", label: "myPurchases", icon: "doc" },
  { href: "/support", label: "helpSupport", icon: "flag" },
];
const privacyUrl = process.env.NEXT_PUBLIC_PRIVACY_URL ?? "https://example.com/privacy";

/** The side menu (same sections as the app's): every page one click away, plus night mode, language and sign out. */
export function SideMenu({ open, onClose, profile, unread }: { open: boolean; onClose: () => void; profile: components["schemas"]["ProfileDto"]; unread: number }) {
  const t = useT();
  const pathname = usePathname();
  const changeLanguage = useChangeLanguage();
  const ref = useRef<HTMLDialogElement>(null);
  const exams = useQuery({ queryKey: ["exams"], queryFn: () => unwrap(api.GET("/api/v1/exams")), enabled: open });
  const exam = exams.data?.find((e) => e.id === profile.targetExamId)?.name;

  useLayoutEffect(() => {
    const d = ref.current;
    if (!d) return;
    if (open && !d.open) d.showModal();
    if (!open && d.open) d.close();
    return () => {
      if (d.open) d.close();
    };
  }, [open]);
  // Following a link closes the menu (also covers back/forward while it is open).
  const lastPath = useRef(pathname);
  useEffect(() => {
    if (pathname !== lastPath.current) {
      lastPath.current = pathname;
      onClose();
    }
  }, [pathname, onClose]);

  const on = (href: string) => pathname === href || pathname.startsWith(href + "/");
  const link = (item: Item, extra?: ReactNode) => (
    <li key={item.href}>
      <Link
        href={item.href}
        onClick={onClose}
        aria-current={on(item.href) ? "page" : undefined}
        className={`flex items-center gap-3 rounded-lg px-3 py-2.5 font-medium ${on(item.href) ? "bg-primary-soft text-primary" : "hover:bg-surface-muted"}`}
      >
        <Icon name={item.icon} className={on(item.href) ? "" : "text-fg-muted"} />
        <span className="flex-1">{t(item.label)}</span>
        {extra}
      </Link>
    </li>
  );
  const section = (title: MessageKey, children: ReactNode) => (
    <section className="border-t border-border px-3 py-3">
      <h2 className="px-3 pb-1 text-xs font-semibold uppercase tracking-wide text-fg-muted">{t(title)}</h2>
      {children}
    </section>
  );

  return (
    <dialog
      ref={ref}
      aria-label={t("menuTitle")}
      onClose={onClose}
      onCancel={(e) => {
        e.preventDefault();
        onClose();
      }}
      // A click on the backdrop lands on the <dialog> itself.
      onClick={(e) => {
        if (e.target === e.currentTarget) onClose();
      }}
      className="fixed inset-y-0 left-0 right-auto m-0 h-dvh max-h-dvh w-80 max-w-[85vw] border-r border-border bg-surface p-0 text-fg shadow-xl transition-transform duration-200 starting:open:-translate-x-full"
    >
      {open && (
        <nav aria-label={t("menuTitle")} className="flex min-h-full flex-col pb-[env(safe-area-inset-bottom)]">
          <div className="flex items-start gap-3 bg-primary-soft px-4 pb-4 pt-[calc(1rem+env(safe-area-inset-top))]">
            <Link href="/profile" onClick={onClose} className="flex min-w-0 flex-1 items-center gap-3 rounded-lg">
              <span className="flex size-12 shrink-0 items-center justify-center rounded-full bg-primary text-lg font-semibold text-primary-fg" aria-hidden>
                {profile.fullName.trim().charAt(0).toUpperCase()}
              </span>
              <span className="min-w-0">
                <span className="block truncate font-semibold">{profile.fullName}</span>
                <span className="line-clamp-2 text-sm text-fg-muted">{exam ? t("preparingFor", { exam, year: profile.targetYear ?? "" }) : (profile.email ?? profile.phone)}</span>
              </span>
            </Link>
            <button type="button" onClick={onClose} aria-label={t("closeMenu")} className="rounded-lg p-1.5 text-fg-muted hover:bg-surface hover:text-fg">
              <Icon name="x" />
            </button>
          </div>
          {section("menuStudy", <ul>{STUDY.map((i) => link(i))}</ul>)}
          {section(
            "menuHelp",
            <ul>
              {HELP.map((i) =>
                link(
                  i,
                  i.href === "/notifications" && unread > 0 ? (
                    <span className="min-w-5 rounded-full bg-danger px-1.5 text-center text-xs font-semibold leading-5 text-white" aria-label={t("unreadCount", { count: unread })}>
                      {unread > 99 ? "99+" : unread}
                    </span>
                  ) : undefined,
                ),
              )}
            </ul>,
          )}
          {section(
            "menuSettings",
            <>
              <NightModeSwitch />
              <div className="flex items-center gap-3 px-3 py-2">
                <Icon name="globe" className="text-fg-muted" />
                <span className="flex-1 font-medium">{t("language")}</span>
                <LanguageSwitch compact onChange={changeLanguage} />
              </div>
              <ul>{link({ href: "/profile/preferences", label: "editPreferences", icon: "target" })}</ul>
              <a href={privacyUrl} target="_blank" rel="noreferrer" className="flex items-center gap-3 rounded-lg px-3 py-2.5 font-medium hover:bg-surface-muted">
                <Icon name="doc" className="text-fg-muted" />
                <span className="flex-1">{t("privacyPolicy")}</span>
                <Icon name="external" size={16} className="text-fg-muted" />
              </a>
            </>,
          )}
          <div className="mt-auto border-t border-border px-3 py-3">
            <button type="button" onClick={() => void signOut()} className="flex w-full items-center gap-3 rounded-lg px-3 py-2.5 text-left font-medium hover:bg-surface-muted">
              <Icon name="logout" className="text-fg-muted" />
              {t("signOut")}
            </button>
          </div>
        </nav>
      )}
    </dialog>
  );
}
