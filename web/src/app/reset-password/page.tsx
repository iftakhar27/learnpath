import type { Metadata } from "next";
import { Suspense } from "react";
import { PageLoading } from "@/components/ui/feedback";
import { StudentResetPassword } from "@/features/student/auth-forms";

export const metadata: Metadata = { title: "Reset password", robots: { index: false } };

export default function Page() {
  return (
    <Suspense fallback={<PageLoading />}>
      <StudentResetPassword />
    </Suspense>
  );
}
