import type { Metadata } from "next";
import { Suspense } from "react";
import { PageLoading } from "@/components/ui/feedback";
import { PlanPage } from "@/features/student/account/plan";

export const metadata: Metadata = { title: "Study plan" };

export default function Page() {
  return (
    <Suspense fallback={<PageLoading />}>
      <PlanPage />
    </Suspense>
  );
}
