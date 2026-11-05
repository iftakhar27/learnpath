import type { Metadata } from "next";
import { Suspense } from "react";
import { PageLoading } from "@/components/ui/feedback";
import { ReviewPage } from "@/features/student/tests/results";

export const metadata: Metadata = { title: "Review answers" };

export default function Page() {
  return (
    <Suspense fallback={<PageLoading />}>
      <ReviewPage />
    </Suspense>
  );
}
