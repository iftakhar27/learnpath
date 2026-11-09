import type { Metadata } from "next";
import { Suspense } from "react";
import { PageLoading } from "@/components/ui/feedback";
import { StudentHandoff } from "@/features/student/auth-forms";

export const metadata: Metadata = { title: "Signing in", robots: { index: false } };

export default function Page() {
  return (
    <Suspense fallback={<PageLoading />}>
      <StudentHandoff />
    </Suspense>
  );
}
