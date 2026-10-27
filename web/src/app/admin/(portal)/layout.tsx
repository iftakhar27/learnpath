import { AdminShell } from "@/components/admin/shell";

export default function PortalLayout({ children }: LayoutProps<"/admin">) {
  return <AdminShell>{children}</AdminShell>;
}
