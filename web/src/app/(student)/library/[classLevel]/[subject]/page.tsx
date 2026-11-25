import type { Metadata } from "next";
import { Suspense } from "react";
import { PageLoading } from "@/components/ui/feedback";
import { SubjectBookPage } from "@/features/student/learn/subjects";

export const metadata: Metadata = { title: "Subject" };

export default function Page() {
  return (
    <Suspense fallback={<PageLoading />}>
      <SubjectBookPage />
    </Suspense>
  );
}
