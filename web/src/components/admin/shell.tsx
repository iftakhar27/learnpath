"use client";

import Link from "next/link";
import { usePathname } from "next/navigation";
import { Suspense, useState, type ReactNode } from "react";
import { can, useLogout, useSession } from "@/lib/session";
import { Button, LinkButton } from "@/components/ui/button";
import { ErrorState, Spinner } from "@/components/ui/feedback";

type NavItem = { href: string; label: string; allowed: (roles?: readonly string[]) => boolean };

const NAV: NavItem[] = [
  { href: "/admin", label: "Dashboard", allowed: can.staff },
  { href: "/admin/catalog", label: "Courses & syllabus", allowed: can.editContent },
  { href: "/admin/questions", label: "Question bank", allowed: can.editContent },
  { href: "/admin/tests", label: "Tests", allowed: can.editContent },
  { href: "/admin/students", label: "Students", allowed: can.support },
  { href: "/admin/support", label: "Support", allowed: (r) => can.support(r) || can.editContent(r) },
  { href: "/admin/orders", label: "Orders", allowed: can.finance },
  { href: "/admin/announcements", label: "Announcements", allowed: can.publishContent },
  { href: "/admin/audit", label: "Audit log", allowed: can.superAdmin },
];

const brand = process.env.NEXT_PUBLIC_BRAND_NAME ?? "LearnPath";

function isActive(pathname: string, href: string) {
  return href === "/admin" ? pathname === "/admin" : pathname === href || pathname.startsWith(href + "/");
}

function NavLinks({ items, onNavigate }: { items: NavItem[]; onNavigate?: () => void }) {
  const pathname = usePathname();
  return <NavList items={items} active={(href) => isActive(pathname, href)} onNavigate={onNavigate} />;
}

function NavList({ items, active, onNavigate }: { items: NavItem[]; active?: (href: string) => boolean; onNavigate?: () => void }) {
  return (
    <ul className="space-y-0.5">
      {items.map((item) => {
        const on = active?.(item.href) ?? false;
        return (
          <li key={item.href}>
            <Link
              href={item.href}
              onClick={onNavigate}
              aria-current={on ? "page" : undefined}
              className={`block rounded-lg px-3 py-2 text-sm font-medium ${on ? "bg-primary-soft text-primary" : "text-fg-muted hover:bg-surface-muted hover:text-fg"}`}
            >
              {item.label}
            </Link>
          </li>
        );
      })}
    </ul>
  );
}

export function AdminShell({ children }: { children: ReactNode }) {
  const session = useSession();
  const logout = useLogout();
  const [menuOpen, setMenuOpen] = useState(false);

  if (session.isPending) {
    return (
      <div className="flex min-h-dvh items-center justify-center text-fg-muted">
        <Spinner />
      </div>
    );
  }
  if (session.isError) {
    return (
      <div className="flex min-h-dvh items-center justify-center">
        <ErrorState error={session.error} onRetry={() => session.refetch()} />
      </div>
    );
  }

  const user = session.data;
  if (!can.staff(user.roles)) {
    return (
      <main className="mx-auto flex min-h-dvh max-w-md flex-col justify-center gap-3 px-4 text-center">
        <h1 className="text-xl font-semibold">No admin access</h1>
        <p className="text-sm text-fg-muted">You&apos;re signed in as {user.fullName}, which isn&apos;t a staff account.</p>
        <Button variant="secondary" onClick={logout}>
          Sign out
        </Button>
      </main>
    );
  }

  const items = NAV.filter((n) => n.allowed(user.roles));
  const sidebar = (onNavigate?: () => void) => (
    <nav aria-label="Admin" className="flex h-full flex-col gap-4 p-3">
      <Link href="/admin" className="px-3 pt-2 text-lg font-semibold tracking-tight" onClick={onNavigate}>
        {brand} <span className="text-sm font-normal text-fg-muted">Admin</span>
      </Link>
      <Suspense fallback={<NavList items={items} />}>
        <NavLinks items={items} onNavigate={onNavigate} />
      </Suspense>
      <div className="mt-auto border-t border-border px-3 pt-3 text-sm">
        <p className="truncate font-medium">{user.fullName}</p>
        <p className="truncate text-xs text-fg-muted">{user.roles.filter((r) => r !== "Student").join(", ")}</p>
        <button type="button" onClick={logout} className="mt-2 text-sm text-primary hover:underline">
          Sign out
        </button>
      </div>
    </nav>
  );

  return (
    <div className="min-h-dvh lg:grid lg:grid-cols-[15rem_1fr]">
      <aside className="sticky top-0 hidden h-dvh border-r border-border bg-surface lg:block">{sidebar()}</aside>

      <header className="sticky top-0 z-20 flex items-center justify-between border-b border-border bg-surface px-4 py-2 lg:hidden">
        <span className="font-semibold">{brand} Admin</span>
        <Button variant="secondary" size="sm" aria-expanded={menuOpen} aria-controls="mobile-nav" onClick={() => setMenuOpen((o) => !o)}>
          {menuOpen ? "Close" : "Menu"}
        </Button>
      </header>
      {menuOpen && (
        <div id="mobile-nav" className="border-b border-border bg-surface lg:hidden">
          {sidebar(() => setMenuOpen(false))}
        </div>
      )}

      <main className="min-w-0 px-4 py-6 sm:px-6 lg:px-8">{children}</main>
    </div>
  );
}

/** Shown on pages a role cannot use (the API would refuse anyway). */
export function RoleGate({ permission, children }: { permission: keyof typeof can; children: ReactNode }) {
  const { data } = useSession();
  if (!data) return null;
  if (!can[permission](data.roles)) {
    return (
      <div className="py-16 text-center">
        <p className="font-medium">You don&apos;t have access to this section.</p>
        <p className="mt-1 text-sm text-fg-muted">Ask a super admin if you need it.</p>
        <LinkButton href="/admin" variant="secondary" className="mt-4">
          Back to dashboard
        </LinkButton>
      </div>
    );
  }
  return <>{children}</>;
}
