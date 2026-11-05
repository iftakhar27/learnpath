"use client";

import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { api, unwrap } from "@/lib/api/client";

type BookmarkType = "Question" | "Resource" | "Topic";
export const bookmarksKey = (type?: BookmarkType) => ["student", "bookmarks", type ?? "all"] as const;

/** Whether an item is saved for revision, and a toggle. */
export function useBookmark(type: BookmarkType, entityId: string) {
  const qc = useQueryClient();
  const list = useQuery({ queryKey: bookmarksKey(type), queryFn: () => unwrap(api.GET("/api/v1/learning/bookmarks", { params: { query: { type } } })) });
  const existing = list.data?.find((b) => b.entityId === entityId);
  const toggle = useMutation({
    mutationFn: async () => {
      if (existing) await unwrap(api.DELETE("/api/v1/learning/bookmarks/{id}", { params: { path: { id: existing.id } } }));
      else await unwrap(api.POST("/api/v1/learning/bookmarks", { body: { entityType: type, entityId } }));
    },
    onSettled: () => qc.invalidateQueries({ queryKey: ["student", "bookmarks"] }),
  });
  return { saved: !!existing, ready: list.isSuccess, toggle };
}
