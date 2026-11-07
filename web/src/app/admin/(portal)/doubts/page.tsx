import type { Metadata } from "next";
import { RoleGate } from "@/components/admin/shell";
import { Doubts } from "@/features/admin/ops/doubts";

export const metadata: Metadata = { title: "Doubts" };

export default function Page() {
  return (
    <RoleGate permission="doubts">
      <Doubts />
    </RoleGate>
  );
}
