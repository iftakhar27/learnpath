import type { Metadata } from "next";
import { Suspense } from "react";
import { PageLoading } from "@/components/ui/feedback";
import { PracticeHome } from "@/features/student/practice/practice";

export const metadata: Metadata = { title: "Practice" };

export default function Page() {
  return (
    <Suspense fallback={<PageLoading />}>
      <PracticeHome />
    </Suspense>
  );
}
