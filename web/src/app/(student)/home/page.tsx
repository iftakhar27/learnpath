import type { Metadata } from "next";
import { StudentHome } from "@/features/student/home";

export const metadata: Metadata = { title: "Home" };

export default function Page() {
  return <StudentHome />;
}
