"use client";

import Link from "next/link";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { api, unwrap } from "@/lib/api/client";
import type { components } from "@/lib/api/schema";
import { useT, type MessageKey } from "@/lib/i18n";
import { PageLoading } from "@/components/ui/feedback";
import { Icon, type IconName } from "@/components/ui/icons";
import { StudentError } from "../errors";
import { bookmarksKey } from "../learn/bookmarks";
import { routes } from "../routes";
import { Muted, PageTitle, useFormat } from "../ui";

type Bookmark = components["schemas"]["BookmarkDto"];
const TYPE: Record<string, { icon: IconName; label: MessageKey; href?: (b: Bookmark) => string }> = {
  Topic: { icon: "book", label: "syllabus", href: (b) => routes.topic(b.entityId) },
  Resource: { icon: "play", label: "video", href: (b) => routes.watch(b.entityId) },
  Question: { icon: "pencil", label: "practiceTitle" },
};

export function BookmarksPage() {
  const t = useT();
  const f = useFormat();
  const qc = useQueryClient();
  const q = useQuery({ queryKey: bookmarksKey(), queryFn: () => unwrap(api.GET("/api/v1/learning/bookmarks")) });
  const remove = useMutation({
    mutationFn: (id: string) => unwrap(api.DELETE("/api/v1/learning/bookmarks/{id}", { params: { path: { id } } })),
    onSuccess: () => qc.invalidateQueries({ queryKey: ["student", "bookmarks"] }),
  });
  if (q.isPending) return <PageLoading />;
  if (q.isError) return <StudentError error={q.error} onRetry={() => q.refetch()} />;

  return (
    <div className="mx-auto max-w-2xl">
      <PageTitle
        actions={
          q.data.some((b) => b.entityType === "Question") && (
            <Link href={routes.practiceMode("bookmarked")} className="text-sm font-medium text-primary hover:underline">
              {t("practiceBookmarked")}
            </Link>
          )
        }
      >
        {t("bookmarksTitle")}
      </PageTitle>
      <StudentError error={remove.error} />
      {q.data.length === 0 ? (
        <Muted>{t("bookmarksEmpty")}</Muted>
      ) : (
        <ul className="divide-y divide-border overflow-hidden rounded-2xl border border-border bg-surface">
          {q.data.map((b) => {
            const type = TYPE[b.entityType] ?? TYPE.Question;
            const content = (
              <>
                <Icon name={type.icon} className="shrink-0 text-fg-muted" />
                <span className="min-w-0 flex-1">
                  <span className="line-clamp-2 font-medium">{b.title}</span>
                  <span className="block text-xs text-fg-muted">{f.date(b.createdAt)}</span>
                </span>
              </>
            );
            return (
              <li key={b.id} className="flex items-center gap-3 px-4 py-3">
                {type.href ? (
                  <Link href={type.href(b)} className="flex min-w-0 flex-1 items-center gap-3 hover:underline">
                    {content}
                  </Link>
                ) : (
                  <span className="flex min-w-0 flex-1 items-center gap-3">{content}</span>
                )}
                <button type="button" onClick={() => remove.mutate(b.id)} disabled={remove.isPending} className="shrink-0 rounded-lg px-2 py-1 text-sm text-fg-muted hover:bg-surface-muted hover:text-danger" aria-label={`${t("remove")}: ${b.title}`}>
                  {t("remove")}
                </button>
              </li>
            );
          })}
        </ul>
      )}
    </div>
  );
}
