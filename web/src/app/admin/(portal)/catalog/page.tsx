import type { Metadata } from "next";
import { RoleGate } from "@/components/admin/shell";
import { CatalogList } from "@/features/admin/catalog/catalog-list";

export const metadata: Metadata = { title: "Courses & syllabus" };

export default function CatalogPage() {
  return (
    <RoleGate permission="editContent">
      <CatalogList />
    </RoleGate>
  );
}
