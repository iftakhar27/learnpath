import type { Metadata } from "next";
import { Suspense } from "react";
import { RoleGate } from "@/components/admin/shell";
import { PageLoading } from "@/components/ui/feedback";
import { TestBuilderPage } from "@/features/admin/tests/test-builder";

export const metadata: Metadata = { title: "New test" };

export default function Page() {
  return (
    <RoleGate permission="editContent">
      <Suspense fallback={<PageLoading />}>
        <TestBuilderPage />
      </Suspense>
    </RoleGate>
  );
}
