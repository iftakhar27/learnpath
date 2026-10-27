import type { Metadata } from "next";
import { RoleGate } from "@/components/admin/shell";
import { Orders } from "@/features/admin/ops/orders";

export const metadata: Metadata = { title: "Orders" };

export default function Page() {
  return (
    <RoleGate permission="finance">
      <Orders />
    </RoleGate>
  );
}
