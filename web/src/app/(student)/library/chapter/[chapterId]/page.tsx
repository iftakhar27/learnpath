import type { Metadata } from "next";
import { Suspense } from "react";
import { PageLoading } from "@/components/ui/feedback";
import { NcertChapterPage } from "@/features/student/learn/subjects";

export const metadata: Metadata = { title: "Chapter" };

export default function Page() {
  return (
    <Suspense fallback={<PageLoading />}>
      <NcertChapterPage />
    </Suspense>
  );
}
