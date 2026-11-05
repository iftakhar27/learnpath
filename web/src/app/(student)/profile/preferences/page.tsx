import type { Metadata } from "next";
import { Suspense } from "react";
import { PageLoading } from "@/components/ui/feedback";
import { PreferencesPage } from "@/features/student/onboarding";

export const metadata: Metadata = { title: "Study preferences" };

export default function Page() {
  return (
    <Suspense fallback={<PageLoading />}>
      <PreferencesPage />
    </Suspense>
  );
}
