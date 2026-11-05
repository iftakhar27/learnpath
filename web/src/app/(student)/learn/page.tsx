import type { Metadata } from "next";
import { Suspense } from "react";
import { PageLoading } from "@/components/ui/feedback";
import { Catalog } from "@/features/student/learn/catalog";

export const metadata: Metadata = { title: "Learn" };

export default function Page() {
  return (
    <Suspense fallback={<PageLoading />}>
      <Catalog />
    </Suspense>
  );
}
