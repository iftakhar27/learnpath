import type { Metadata } from "next";
import { Suspense } from "react";
import { PageLoading } from "@/components/ui/feedback";
import { ProgressPage } from "@/features/student/account/progress";

export const metadata: Metadata = { title: "My progress" };

export default function Page() {
  return (
    <Suspense fallback={<PageLoading />}>
      <ProgressPage />
    </Suspense>
  );
}
