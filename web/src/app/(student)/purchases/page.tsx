import type { Metadata } from "next";
import { Suspense } from "react";
import { PageLoading } from "@/components/ui/feedback";
import { PurchasesPage } from "@/features/student/account/purchases";

export const metadata: Metadata = { title: "My purchases" };

export default function Page() {
  return (
    <Suspense fallback={<PageLoading />}>
      <PurchasesPage />
    </Suspense>
  );
}
