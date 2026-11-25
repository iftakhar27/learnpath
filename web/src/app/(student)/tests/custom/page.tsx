import type { Metadata } from "next";
import { Suspense } from "react";
import { PageLoading } from "@/components/ui/feedback";
import { CustomTestGenerator } from "@/features/student/tests/custom-test";

export const metadata: Metadata = { title: "Create a custom test" };

export default function Page() {
  return (
    <Suspense fallback={<PageLoading />}>
      <CustomTestGenerator />
    </Suspense>
  );
}
