import type { Metadata } from "next";
import { Suspense } from "react";
import { PageLoading } from "@/components/ui/feedback";
import { DoubtsPage } from "@/features/student/doubts/doubts";

export const metadata: Metadata = { title: "My doubts" };

export default function Page() {
  return (
    <Suspense fallback={<PageLoading />}>
      <DoubtsPage />
    </Suspense>
  );
}
