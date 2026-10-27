"use client";

import Link from "next/link";
import { usePathname, useRouter, useSearchParams } from "next/navigation";
import { keepPreviousData, useQuery } from "@tanstack/react-query";
import { api, unwrap } from "@/lib/api/client";
import { ROLES } from "@/lib/api/types";
import { formatDateTime } from "@/lib/format";
import { Badge, EmptyState, ErrorState, SkeletonRows, StatusBadge } from "@/components/ui/feedback";
import { Field, Select } from "@/components/ui/field";
import { SearchInput } from "@/components/ui/search-input";
import { Card, PageHeader, Pagination, Table, Td, Th } from "@/components/ui/layout";

export function StudentList() {
  const params = useSearchParams();
  const router = useRouter();
  const pathname = usePathname();
  const q = params.get("q") ?? "";
  const role = params.get("role") ?? "";
  const page = Number(params.get("page")) || 1;

  const update = (patch: Record<string, string | number>) => {
    const next = new URLSearchParams(params.toString());
    for (const [k, v] of Object.entries({ page: 1, ...patch } as Record<string, string | number>)) {
      if (v === "" || (k === "page" && v === 1)) next.delete(k);
      else next.set(k, String(v));
    }
    router.replace(`${pathname}?${next}`, { scroll: false });
  };

  const users = useQuery({
    queryKey: ["admin", "users", { q, role, page }],
    placeholderData: keepPreviousData,
    queryFn: () => unwrap(api.GET("/api/v1/admin/users", { params: { query: { q: q || undefined, role: role || undefined, page } } })),
  });

  return (
    <>
      <PageHeader title="Students & staff" description="Look up an account to see its courses, payments and support history." />
      <Card className="mb-4 p-4">
        <div className="grid gap-3 sm:grid-cols-[1fr_14rem]">
          <SearchInput key={q} id="us-q" label="Search" hint="Name, email or phone number. Press Enter." value={q} onSearch={(v) => update({ q: v })} />
          <Field label="Role" htmlFor="us-role">
            <Select id="us-role" value={role} onChange={(e) => update({ role: e.target.value })}>
              <option value="">Any role</option>
              {ROLES.map((r) => (
                <option key={r}>{r}</option>
              ))}
            </Select>
          </Field>
        </div>
      </Card>
      <Card>
        {users.isPending ? (
          <SkeletonRows />
        ) : users.isError ? (
          <ErrorState error={users.error} onRetry={() => users.refetch()} />
        ) : users.data.items.length === 0 ? (
          <EmptyState title="No accounts found">Try a different search.</EmptyState>
        ) : (
          <>
            <Table>
              <thead>
                <tr>
                  <Th>Name</Th>
                  <Th>Contact</Th>
                  <Th>Roles</Th>
                  <Th>Status</Th>
                  <Th>Joined</Th>
                  <Th>Last sign-in</Th>
                </tr>
              </thead>
              <tbody className={users.isPlaceholderData ? "opacity-60" : ""}>
                {users.data.items.map((u) => (
                  <tr key={u.id} className="hover:bg-surface-muted/50">
                    <Td>
                      <Link href={`/admin/students/${u.id}`} className="font-medium text-primary hover:underline">
                        {u.fullName}
                      </Link>
                    </Td>
                    <Td>
                      <div>{u.email ?? "—"}</div>
                      <div className="text-xs text-fg-muted">{u.phone}</div>
                    </Td>
                    <Td>
                      <div className="flex flex-wrap gap-1">
                        {u.roles.map((r) => (
                          <Badge key={r} tone={r === "Student" ? "neutral" : "primary"}>
                            {r}
                          </Badge>
                        ))}
                      </div>
                    </Td>
                    <Td>
                      <StatusBadge status={u.status} />
                    </Td>
                    <Td className="whitespace-nowrap text-fg-muted">{formatDateTime(u.createdAt)}</Td>
                    <Td className="whitespace-nowrap text-fg-muted">{formatDateTime(u.lastLoginAt)}</Td>
                  </tr>
                ))}
              </tbody>
            </Table>
            <Pagination page={users.data.page} pageSize={users.data.pageSize} total={users.data.total} onPage={(p) => update({ page: p })} />
          </>
        )}
      </Card>
    </>
  );
}
