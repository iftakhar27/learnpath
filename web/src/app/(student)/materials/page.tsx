import type { Metadata } from "next";
import { Suspense } from "react";
import { PageLoading } from "@/components/ui/feedback";
import { MaterialsPage } from "@/features/student/materials";

export const metadata: Metadata = { title: "Free study material" };

export default function Page() {
  return (
    <Suspense fallback={<PageLoading />}>
      <MaterialsPage />
    </Suspense>
  );
}
