import type { Metadata } from "next";
import { Suspense } from "react";
import { RoleGate } from "@/components/admin/shell";
import { PageLoading } from "@/components/ui/feedback";
import { StudentDetail } from "@/features/admin/students/student-detail";

export const metadata: Metadata = { title: "Student" };

export default function Page() {
  return (
    <RoleGate permission="support">
      <Suspense fallback={<PageLoading />}>
        <StudentDetail />
      </Suspense>
    </RoleGate>
  );
}
