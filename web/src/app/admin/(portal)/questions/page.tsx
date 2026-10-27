import type { Metadata } from "next";
import { Suspense } from "react";
import { RoleGate } from "@/components/admin/shell";
import { PageLoading } from "@/components/ui/feedback";
import { QuestionList } from "@/features/admin/questions/question-list";

export const metadata: Metadata = { title: "Question bank" };

export default function Page() {
  return (
    <RoleGate permission="editContent">
      <Suspense fallback={<PageLoading />}>
        <QuestionList />
      </Suspense>
    </RoleGate>
  );
}
