import type { Metadata } from "next";
import { Suspense } from "react";
import { RoleGate } from "@/components/admin/shell";
import { PageLoading } from "@/components/ui/feedback";
import { QuestionEditorPage } from "@/features/admin/questions/question-editor";

export const metadata: Metadata = { title: "Edit question" };

export default function Page() {
  return (
    <RoleGate permission="editContent">
      <Suspense fallback={<PageLoading />}>
        <QuestionEditorPage />
      </Suspense>
    </RoleGate>
  );
}
