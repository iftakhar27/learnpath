import type { Metadata } from "next";
import { Suspense } from "react";
import { PageLoading } from "@/components/ui/feedback";
import { NotificationsPage } from "@/features/student/account/notifications";

export const metadata: Metadata = { title: "Notifications" };

export default function Page() {
  return (
    <Suspense fallback={<PageLoading />}>
      <NotificationsPage />
    </Suspense>
  );
}
