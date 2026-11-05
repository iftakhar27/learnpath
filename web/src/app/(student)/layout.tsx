import { Suspense } from "react";
import { Spinner } from "@/components/ui/feedback";
import { StudentShell } from "@/features/student/shell";

/** The shell reads the URL (navigation state), which isn't known while pages with parameters are prerendered. */
export default function StudentLayout({ children }: { children: React.ReactNode }) {
  return (
    <Suspense
      fallback={
        <div className="flex min-h-dvh items-center justify-center text-fg-muted">
          <Spinner />
        </div>
      }
    >
      <StudentShell>{children}</StudentShell>
    </Suspense>
  );
}
