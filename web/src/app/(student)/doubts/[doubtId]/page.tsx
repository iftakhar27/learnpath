import type { Metadata } from "next";
import { Suspense } from "react";
import { PageLoading } from "@/components/ui/feedback";
import { DoubtPage } from "@/features/student/doubts/doubts";

export const metadata: Metadata = { title: "Doubt" };

export default function Page() {
  return (
    <Suspense fallback={<PageLoading />}>
      <DoubtPage />
    </Suspense>
  );
}
