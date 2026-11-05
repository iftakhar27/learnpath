import type { Metadata } from "next";
import { Suspense } from "react";
import { PageLoading } from "@/components/ui/feedback";
import { SupportPage } from "@/features/student/account/support";

export const metadata: Metadata = { title: "Help & support" };

export default function Page() {
  return (
    <Suspense fallback={<PageLoading />}>
      <SupportPage />
    </Suspense>
  );
}
