import type { Metadata } from "next";
import { RoleGate } from "@/components/admin/shell";
import { Announcements } from "@/features/admin/ops/announcements";

export const metadata: Metadata = { title: "Announcements" };

export default function Page() {
  return (
    <RoleGate permission="publishContent">
      <Announcements />
    </RoleGate>
  );
}
