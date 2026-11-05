import type { Metadata } from "next";
import { Suspense } from "react";
import { PageLoading } from "@/components/ui/feedback";
import { ProfilePage } from "@/features/student/account/profile";

export const metadata: Metadata = { title: "Profile" };

export default function Page() {
  return (
    <Suspense fallback={<PageLoading />}>
      <ProfilePage />
    </Suspense>
  );
}
