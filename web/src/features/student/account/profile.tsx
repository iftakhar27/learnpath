"use client";

import Link from "next/link";
import { useState } from "react";
import { useQuery } from "@tanstack/react-query";
import { api, unwrap } from "@/lib/api/client";
import { useT, type MessageKey } from "@/lib/i18n";
import { Button } from "@/components/ui/button";
import { Modal } from "@/components/ui/dialog";
import { PageLoading } from "@/components/ui/feedback";
import { Icon, type IconName } from "@/components/ui/icons";
import { StudentError } from "../errors";
import { LanguageSwitch } from "../language-switch";
import { useChangeLanguage, useProfile } from "../profile";

const LINKS: { href: string; label: MessageKey; icon: IconName }[] = [
  { href: "/profile/preferences", label: "editPreferences", icon: "target" },
  { href: "/progress", label: "myProgress", icon: "check" },
  { href: "/plan", label: "planTitle", icon: "clock" },
  { href: "/bookmarks", label: "bookmarksTitle", icon: "bookmark" },
  { href: "/purchases", label: "myPurchases", icon: "doc" },
  { href: "/notifications", label: "notificationsTitle", icon: "bell" },
  { href: "/support", label: "helpSupport", icon: "flag" },
];
const privacyUrl = process.env.NEXT_PUBLIC_PRIVACY_URL ?? "https://example.com/privacy";

async function signOut(to = "/login") {
  await fetch("/api/session/logout", { method: "POST" }).catch(() => undefined);
  // A full load clears every cached query from this session.
  window.location.assign(to);
}

export function ProfilePage() {
  const t = useT();
  const profile = useProfile();
  const changeLanguage = useChangeLanguage();
  const exams = useQuery({ queryKey: ["exams"], queryFn: () => unwrap(api.GET("/api/v1/exams")) });
  const [confirmDelete, setConfirmDelete] = useState(false);
  const [deleting, setDeleting] = useState(false);
  const [error, setError] = useState<unknown>(null);

  if (profile.isPending) return <PageLoading />;
  if (profile.isError) return <StudentError error={profile.error} onRetry={() => profile.refetch()} />;
  const p = profile.data;
  const exam = exams.data?.find((e) => e.id === p.targetExamId)?.name;

  const requestDeletion = async () => {
    setDeleting(true);
    setError(null);
    try {
      await unwrap(api.POST("/api/v1/student/profile/delete-request"));
      await signOut("/login?deleted=1");
    } catch (e) {
      setError(e);
      setDeleting(false);
    }
  };

  return (
    <div className="mx-auto max-w-2xl space-y-6">
      <section className="flex items-center gap-4 rounded-2xl border border-border bg-surface p-5">
        <span className="flex size-14 shrink-0 items-center justify-center rounded-full bg-primary-soft text-xl font-semibold text-primary" aria-hidden>
          {p.fullName.trim().charAt(0).toUpperCase()}
        </span>
        <div className="min-w-0">
          <h1 className="truncate text-xl font-semibold">{p.fullName}</h1>
          <p className="truncate text-sm text-fg-muted">{p.email ?? p.phone}</p>
          {exam && <p className="mt-1 text-sm">{t("preparingFor", { exam, year: p.targetYear ?? "" })}</p>}
        </div>
      </section>

      <nav aria-label={t("profileTitle")} className="overflow-hidden rounded-2xl border border-border bg-surface">
        <ul className="divide-y divide-border">
          {LINKS.map((l) => (
            <li key={l.href}>
              <Link href={l.href} className="flex items-center gap-3 px-5 py-3.5 hover:bg-surface-muted">
                <Icon name={l.icon} className="text-fg-muted" />
                <span className="flex-1 font-medium">{t(l.label)}</span>
                <Icon name="chevronRight" size={16} className="text-fg-muted" />
              </Link>
            </li>
          ))}
        </ul>
      </nav>

      <section className="flex flex-wrap items-center justify-between gap-3 rounded-2xl border border-border bg-surface p-5">
        <span className="font-medium">{t("language")}</span>
        <LanguageSwitch onChange={changeLanguage} />
      </section>

      <div className="flex flex-wrap items-center justify-between gap-3">
        <Button variant="secondary" onClick={() => void signOut()}>
          {t("signOut")}
        </Button>
        <a href={privacyUrl} target="_blank" rel="noreferrer" className="text-sm text-fg-muted hover:underline">
          {t("privacyPolicy")}
        </a>
      </div>
      <button type="button" onClick={() => setConfirmDelete(true)} className="text-sm font-medium text-danger hover:underline">
        {t("deleteAccount")}
      </button>

      <Modal
        open={confirmDelete}
        onClose={() => setConfirmDelete(false)}
        title={t("deleteAccount")}
        closeLabel={t("cancel")}
        footer={
          <>
            <Button variant="secondary" onClick={() => setConfirmDelete(false)}>
              {t("cancel")}
            </Button>
            <Button variant="danger" onClick={requestDeletion} loading={deleting}>
              {t("delete")}
            </Button>
          </>
        }
      >
        <div className="space-y-3">
          <p>{t("deleteAccountBody")}</p>
          <StudentError error={error} />
        </div>
      </Modal>
    </div>
  );
}
