import type { Metadata } from "next";
import { Suspense } from "react";
import { Onboarding } from "@/features/student/onboarding";

export const metadata: Metadata = { title: "Set up" };

export default function Page() {
  return (
    <Suspense>
      <Onboarding />
    </Suspense>
  );
}
