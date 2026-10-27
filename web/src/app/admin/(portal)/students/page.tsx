import type { Metadata } from "next";
import { Suspense } from "react";
import { RoleGate } from "@/components/admin/shell";
import { PageLoading } from "@/components/ui/feedback";
import { StudentList } from "@/features/admin/students/student-list";

export const metadata: Metadata = { title: "Students" };

export default function Page() {
  return (
    <RoleGate permission="support">
      <Suspense fallback={<PageLoading />}>
        <StudentList />
      </Suspense>
    </RoleGate>
  );
}
