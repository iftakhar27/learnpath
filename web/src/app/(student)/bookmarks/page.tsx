import type { Metadata } from "next";
import { Suspense } from "react";
import { PageLoading } from "@/components/ui/feedback";
import { BookmarksPage } from "@/features/student/account/bookmarks";

export const metadata: Metadata = { title: "Saved for revision" };

export default function Page() {
  return (
    <Suspense fallback={<PageLoading />}>
      <BookmarksPage />
    </Suspense>
  );
}
