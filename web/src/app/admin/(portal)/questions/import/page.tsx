import type { Metadata } from "next";
import { RoleGate } from "@/components/admin/shell";
import { QuestionImport } from "@/features/admin/questions/question-import";

export const metadata: Metadata = { title: "Import questions" };

export default function Page() {
  return (
    <RoleGate permission="editContent">
      <QuestionImport />
    </RoleGate>
  );
}
