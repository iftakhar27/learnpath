import type { Metadata } from "next";
import { Suspense } from "react";
import { PageLoading } from "@/components/ui/feedback";
import { WatchPage } from "@/features/student/learn/watch";

export const metadata: Metadata = { title: "Lesson" };

export default function Page() {
  return (
    <Suspense fallback={<PageLoading />}>
      <WatchPage />
    </Suspense>
  );
}
