import type { Metadata } from "next";
import { Suspense } from "react";
import { StudentLogin } from "@/features/student/auth-forms";

export const metadata: Metadata = { title: "Sign in" };

export default function Page() {
  return (
    <Suspense>
      <StudentLogin />
    </Suspense>
  );
}
