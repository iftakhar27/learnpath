import type { Metadata } from "next";
import { RoleGate } from "@/components/admin/shell";
import { Support } from "@/features/admin/ops/support";

export const metadata: Metadata = { title: "Support" };

export default function Page() {
  return (
    <RoleGate permission="supportPage">
      <Support />
    </RoleGate>
  );
}
