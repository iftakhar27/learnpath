import type { Metadata } from "next";
import { Suspense } from "react";
import { RoleGate } from "@/components/admin/shell";
import { SkeletonRows } from "@/components/ui/feedback";
import { DoubtThread } from "@/features/admin/ops/doubts";

export const metadata: Metadata = { title: "Doubt" };

export default function Page() {
  return (
    <RoleGate permission="doubts">
      <Suspense fallback={<SkeletonRows />}>
        <DoubtThread />
      </Suspense>
    </RoleGate>
  );
}
