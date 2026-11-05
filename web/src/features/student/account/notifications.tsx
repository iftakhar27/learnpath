"use client";

import { useRouter } from "next/navigation";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { api, unwrap } from "@/lib/api/client";
import type { components } from "@/lib/api/schema";
import { useT } from "@/lib/i18n";
import { Button } from "@/components/ui/button";
import { PageLoading } from "@/components/ui/feedback";
import { StudentError } from "../errors";
import { notificationsKey } from "../shell";
import { Muted, PageTitle, useFormat } from "../ui";

type Notification = components["schemas"]["NotificationDto"];

/** Only links into this site are followed. */
const internal = (link?: string | null) => (link && link.startsWith("/") && !link.startsWith("//") ? link : null);

export function NotificationsPage() {
  const t = useT();
  const f = useFormat();
  const router = useRouter();
  const qc = useQueryClient();
  const q = useQuery({ queryKey: notificationsKey, queryFn: () => unwrap(api.GET("/api/v1/notifications")) });
  const refresh = () => qc.invalidateQueries({ queryKey: notificationsKey });
  const readAll = useMutation({ mutationFn: () => unwrap(api.POST("/api/v1/notifications/read-all")), onSuccess: refresh });
  const open = async (n: Notification) => {
    if (!n.read) {
      await unwrap(api.POST("/api/v1/notifications/{id}/read", { params: { path: { id: n.id } } })).catch(() => undefined);
      void refresh();
    }
    const link = internal(n.link);
    if (link) router.push(link);
  };

  if (q.isPending) return <PageLoading />;
  if (q.isError) return <StudentError error={q.error} onRetry={() => q.refetch()} />;
  const items = q.data.items;
  const unread = items.filter((n) => !n.read).length;

  return (
    <div className="mx-auto max-w-2xl">
      <PageTitle
        actions={
          unread > 0 && (
            <Button variant="secondary" size="sm" onClick={() => readAll.mutate()} loading={readAll.isPending}>
              {t("markAllRead")}
            </Button>
          )
        }
      >
        {t("notificationsTitle")}
      </PageTitle>
      {items.length === 0 ? (
        <Muted>{t("notificationsEmpty")}</Muted>
      ) : (
        <ul className="space-y-2">
          {items.map((n) => (
            <li key={n.id}>
              <button
                type="button"
                onClick={() => void open(n)}
                className={`w-full rounded-2xl border p-4 text-left hover:border-primary ${n.read ? "border-border bg-surface" : "border-primary/40 bg-primary-soft"}`}
              >
                <span className="flex items-start justify-between gap-3">
                  <span className="font-medium">
                    {!n.read && <span className="sr-only">{t("unread")}: </span>}
                    {n.title}
                  </span>
                  <span className="shrink-0 text-xs text-fg-muted">{f.dateTime(n.createdAt)}</span>
                </span>
                <span className="mt-1 block whitespace-pre-line text-sm text-fg-muted">{n.body}</span>
              </button>
            </li>
          ))}
        </ul>
      )}
    </div>
  );
}
