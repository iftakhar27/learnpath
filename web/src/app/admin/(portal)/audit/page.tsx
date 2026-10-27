import type { Metadata } from "next";
import { RoleGate } from "@/components/admin/shell";
import { AuditLog } from "@/features/admin/ops/audit";

export const metadata: Metadata = { title: "Audit log" };

export default function Page() {
  return (
    <RoleGate permission="superAdmin">
      <AuditLog />
    </RoleGate>
  );
}
