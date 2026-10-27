import type { Metadata } from "next";
import { Suspense } from "react";
import { RoleGate } from "@/components/admin/shell";
import { PageLoading } from "@/components/ui/feedback";
import { CourseEditor } from "@/features/admin/catalog/course-editor";

export const metadata: Metadata = { title: "Edit course" };

export default function CourseEditorPage() {
  return (
    <RoleGate permission="editContent">
      <Suspense fallback={<PageLoading />}>
        <CourseEditor />
      </Suspense>
    </RoleGate>
  );
}
