import type { Metadata } from "next";
import { Suspense } from "react";
import { PageLoading } from "@/components/ui/feedback";
import { CoursePage } from "@/features/student/learn/course";

export const metadata: Metadata = { title: "Course" };

export default function Page() {
  return (
    <Suspense fallback={<PageLoading />}>
      <CoursePage />
    </Suspense>
  );
}
