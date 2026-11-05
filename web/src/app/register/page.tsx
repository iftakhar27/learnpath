import type { Metadata } from "next";
import { Suspense } from "react";
import { StudentRegister } from "@/features/student/auth-forms";

export const metadata: Metadata = { title: "Create account" };

export default function Page() {
  return (
    <Suspense>
      <StudentRegister />
    </Suspense>
  );
}
