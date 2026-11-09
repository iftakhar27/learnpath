import type { Metadata } from "next";
import { Suspense } from "react";
import { PageLoading } from "@/components/ui/feedback";
import { StudentForgotPassword } from "@/features/student/auth-forms";

export const metadata: Metadata = { title: "Forgot password", robots: { index: false } };

export default function Page() {
  return (
    <Suspense fallback={<PageLoading />}>
      <StudentForgotPassword />
    </Suspense>
  );
}
