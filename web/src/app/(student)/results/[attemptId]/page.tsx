import type { Metadata } from "next";
import { Suspense } from "react";
import { PageLoading } from "@/components/ui/feedback";
import { ResultPage } from "@/features/student/tests/results";

export const metadata: Metadata = { title: "Result" };

export default function Page() {
  return (
    <Suspense fallback={<PageLoading />}>
      <ResultPage />
    </Suspense>
  );
}
