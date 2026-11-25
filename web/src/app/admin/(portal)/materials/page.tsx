import type { Metadata } from "next";
import { RoleGate } from "@/components/admin/shell";
import { MaterialsAdmin } from "@/features/admin/materials";

export const metadata: Metadata = { title: "Free material" };

export default function Page() {
  return (
    <RoleGate permission="editContent">
      <MaterialsAdmin />
    </RoleGate>
  );
}
