import type { Metadata } from "next";
import { Suspense } from "react";
import { RoleGate } from "@/components/admin/shell";
import { PageLoading } from "@/components/ui/feedback";
import { TestList } from "@/features/admin/tests/test-list";

export const metadata: Metadata = { title: "Tests" };

export default function Page() {
  return (
    <RoleGate permission="editContent">
      <Suspense fallback={<PageLoading />}>
        <TestList />
      </Suspense>
    </RoleGate>
  );
}
