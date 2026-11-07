import type { Metadata } from "next";
import { Suspense } from "react";
import { PageLoading } from "@/components/ui/feedback";
import { TopicQuizPage } from "@/features/student/learn/quiz";

export const metadata: Metadata = { title: "Topic quiz" };

export default function Page() {
  return (
    <Suspense fallback={<PageLoading />}>
      <TopicQuizPage />
    </Suspense>
  );
}
