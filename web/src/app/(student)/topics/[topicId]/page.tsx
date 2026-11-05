import type { Metadata } from "next";
import { Suspense } from "react";
import { PageLoading } from "@/components/ui/feedback";
import { TopicPage } from "@/features/student/learn/topic";

export const metadata: Metadata = { title: "Topic" };

export default function Page() {
  return (
    <Suspense fallback={<PageLoading />}>
      <TopicPage />
    </Suspense>
  );
}
