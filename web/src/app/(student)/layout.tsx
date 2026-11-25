import { cookies } from "next/headers";
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
      <Shell>{children}</Shell>
    </Suspense>
  );
}

/**
 * Without a session cookie the visitor is a guest: only the public pages (catalogue, courses, NCERT subjects) get this
 * far, as the proxy sends them to sign in for the rest. Reading the request's cookies happens inside the Suspense
 * boundary above, as Cache Components requires.
 */
async function Shell({ children }: { children: React.ReactNode }) {
  const guest = !(await cookies()).has("lp_rt");
  return <StudentShell guest={guest}>{children}</StudentShell>;
}
