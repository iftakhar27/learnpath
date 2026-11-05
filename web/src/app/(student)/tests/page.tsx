import type { Metadata } from "next";
import { Suspense } from "react";
import { PageLoading } from "@/components/ui/feedback";
import { TestsList } from "@/features/student/tests/tests";

export const metadata: Metadata = { title: "Tests" };

export default function Page() {
  return (
    <Suspense fallback={<PageLoading />}>
      <TestsList />
    </Suspense>
  );
}
