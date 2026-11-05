import type { Metadata } from "next";
import { Suspense } from "react";
import { PageLoading } from "@/components/ui/feedback";
import { ExamPage } from "@/features/student/tests/exam";

export const metadata: Metadata = { title: "Test" };

export default function Page() {
  return (
    <Suspense fallback={<PageLoading />}>
      <ExamPage />
    </Suspense>
  );
}
