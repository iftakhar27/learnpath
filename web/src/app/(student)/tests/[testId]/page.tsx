import type { Metadata } from "next";
import { Suspense } from "react";
import { PageLoading } from "@/components/ui/feedback";
import { TestInstructions } from "@/features/student/tests/tests";

export const metadata: Metadata = { title: "Test" };

export default function Page() {
  return (
    <Suspense fallback={<PageLoading />}>
      <TestInstructions />
    </Suspense>
  );
}
