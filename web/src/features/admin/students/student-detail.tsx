"use client";

import { useParams } from "next/navigation";
import { useState } from "react";
import { useQuery } from "@tanstack/react-query";
import { api, unwrap } from "@/lib/api/client";
import { ROLES, USER_STATUSES, type UserStatus } from "@/lib/api/types";
import { formatDateTime, formatPaise } from "@/lib/format";
import { can, useSession } from "@/lib/session";
import { Button } from "@/components/ui/button";
import { ConfirmDialog, Modal } from "@/components/ui/dialog";
import { Badge, EmptyState, ErrorState, PageLoading, StatusBadge } from "@/components/ui/feedback";
import { Field, Select, Textarea } from "@/components/ui/field";
import { Card, PageHeader, Table, Td, Th } from "@/components/ui/layout";
import { BackLink, CourseSelect, useAction } from "../shared";

export function StudentDetail() {
  const { id } = useParams<{ id: string }>();
  const { data: me } = useSession();
  const key = ["admin", "user", id];
  const detail = useQuery({ queryKey: key, queryFn: () => unwrap(api.GET("/api/v1/admin/users/{id}", { params: { path: { id } } })) });
  const [status, setStatus] = useState<UserStatus | null>(null);
  const [granting, setGranting] = useState(false);
  const [grantCourse, setGrantCourse] = useState("");
  const [grantReason, setGrantReason] = useState("");
  const [addRole, setAddRole] = useState("");

  const invalidate = [key, ["admin", "users"]];
  const changeStatus = useAction((s: UserStatus) => unwrap(api.POST("/api/v1/admin/users/{id}/status", { params: { path: { id } }, body: { status: s } })), {
    invalidate,
    success: "Account status updated",
    onSuccess: () => setStatus(null),
  });
  const grant = useAction(() => unwrap(api.POST("/api/v1/admin/users/{id}/enrollments", { params: { path: { id } }, body: { courseId: grantCourse, reason: grantReason || null } })), {
    invalidate,
    success: "Course access granted",
    onSuccess: () => {
      setGranting(false);
      setGrantCourse("");
      setGrantReason("");
    },
  });
  const roleAdd = useAction((role: string) => unwrap(api.POST("/api/v1/admin/users/{id}/roles", { params: { path: { id } }, body: { role } })), {
    invalidate,
    success: "Role added. It applies the next time they sign in.",
    onSuccess: () => setAddRole(""),
  });
  const roleRemove = useAction((role: string) => unwrap(api.DELETE("/api/v1/admin/users/{id}/roles/{role}", { params: { path: { id, role } } })), {
    invalidate,
    success: "Role removed",
  });

  if (detail.isPending) return <PageLoading />;
  if (detail.isError) return <ErrorState error={detail.error} onRetry={() => detail.refetch()} />;
  const { user, enrollments, orders, tickets } = detail.data;
  const isMe = me?.userId === user.id;

  return (
    <>
      <PageHeader
        back={<BackLink href="/admin/students">Students & staff</BackLink>}
        title={
          <span className="flex flex-wrap items-center gap-2">
            {user.fullName} <StatusBadge status={user.status} />
          </span>
        }
        description={`Joined ${formatDateTime(user.createdAt)} · last sign-in ${formatDateTime(user.lastLoginAt)}`}
        actions={<Button onClick={() => setGranting(true)}>Grant course access</Button>}
      />

      <div className="grid gap-6 lg:grid-cols-[1fr_20rem]">
        <div className="space-y-6">
          <Card title="Courses">
            {enrollments.length === 0 ? (
              <EmptyState title="Not enrolled in any course" />
            ) : (
              <Table>
                <thead>
                  <tr>
                    <Th>Course</Th>
                    <Th>Source</Th>
                    <Th>From</Th>
                    <Th>Until</Th>
                  </tr>
                </thead>
                <tbody>
                  {enrollments.map((e) => (
                    <tr key={e.courseId}>
                      <Td>{e.courseTitle}</Td>
                      <Td>
                        <Badge tone={e.source === "Purchase" ? "success" : e.source === "Admin" ? "primary" : "neutral"}>{e.source}</Badge>
                      </Td>
                      <Td className="whitespace-nowrap">{formatDateTime(e.startsAt)}</Td>
                      <Td className="whitespace-nowrap">{e.expiresAt ? formatDateTime(e.expiresAt) : "Lifetime"}</Td>
                    </tr>
                  ))}
                </tbody>
              </Table>
            )}
          </Card>

          <Card title="Orders">
            {orders.length === 0 ? (
              <EmptyState title="No orders" />
            ) : (
              <Table>
                <thead>
                  <tr>
                    <Th>Course</Th>
                    <Th className="text-right">Amount</Th>
                    <Th>Status</Th>
                    <Th>Gateway order</Th>
                    <Th>Created</Th>
                  </tr>
                </thead>
                <tbody>
                  {orders.map((o) => (
                    <tr key={o.id}>
                      <Td>{o.courseTitle}</Td>
                      <Td className="text-right tabular-nums">{formatPaise(o.amountInPaise)}</Td>
                      <Td>
                        <StatusBadge status={o.status} />
                      </Td>
                      <Td className="font-mono text-xs">{o.gatewayOrderId}</Td>
                      <Td className="whitespace-nowrap">{formatDateTime(o.createdAt)}</Td>
                    </tr>
                  ))}
                </tbody>
              </Table>
            )}
          </Card>

          <Card title="Support tickets">
            {tickets.length === 0 ? (
              <EmptyState title="No tickets" />
            ) : (
              <ul className="divide-y divide-border">
                {tickets.map((t) => (
                  <li key={t.id} className="flex items-center justify-between gap-2 px-4 py-2.5 text-sm">
                    <span>{t.subject}</span>
                    <span className="flex items-center gap-2 text-fg-muted">
                      {formatDateTime(t.createdAt)} <StatusBadge status={t.status} />
                    </span>
                  </li>
                ))}
              </ul>
            )}
          </Card>
        </div>

        <div className="space-y-6">
          <Card title="Contact" className="p-0">
            <dl className="space-y-2 p-4 text-sm">
              <div>
                <dt className="text-fg-muted">Email</dt>
                <dd>{user.email ?? "—"}</dd>
              </div>
              <div>
                <dt className="text-fg-muted">Phone</dt>
                <dd>{user.phone ?? "—"}</dd>
              </div>
            </dl>
          </Card>

          <Card title="Account status" className="p-0">
            <div className="space-y-3 p-4">
              <Select aria-label="Account status" value={user.status} disabled={isMe} onChange={(e) => setStatus(e.target.value as UserStatus)}>
                {USER_STATUSES.map((s) => (
                  <option key={s} value={s}>
                    {s.replace(/([a-z])([A-Z])/g, "$1 $2")}
                  </option>
                ))}
              </Select>
              <p className="text-xs text-fg-muted">{isMe ? "You can't change your own status." : "Suspending signs the user out everywhere."}</p>
            </div>
          </Card>

          <Card title="Roles" className="p-0">
            <div className="space-y-3 p-4">
              <div className="flex flex-wrap gap-1.5">
                {user.roles.map((r) => (
                  <span key={r} className="inline-flex items-center gap-1 rounded-full bg-surface-muted px-2.5 py-1 text-xs font-medium">
                    {r}
                    {can.superAdmin(me?.roles) && !(isMe && r === "SuperAdmin") && (
                      <button type="button" aria-label={`Remove role ${r}`} className="text-fg-muted hover:text-danger" onClick={() => roleRemove.mutate(r)}>
                        ✕
                      </button>
                    )}
                  </span>
                ))}
              </div>
              {can.superAdmin(me?.roles) ? (
                <div className="flex gap-2">
                  <Select aria-label="Role to add" value={addRole} onChange={(e) => setAddRole(e.target.value)}>
                    <option value="">Add a role…</option>
                    {ROLES.filter((r) => !user.roles.includes(r)).map((r) => (
                      <option key={r}>{r}</option>
                    ))}
                  </Select>
                  <Button variant="secondary" disabled={!addRole} loading={roleAdd.isPending} onClick={() => roleAdd.mutate(addRole)}>
                    Add
                  </Button>
                </div>
              ) : (
                <p className="text-xs text-fg-muted">Only super admins can change roles.</p>
              )}
            </div>
          </Card>
        </div>
      </div>

      <ConfirmDialog
        open={status !== null}
        title="Change account status?"
        message={<>Set {user.fullName}&apos;s account to <strong>{status}</strong>?{status !== "Active" && " They will be signed out of every device."}</>}
        confirmLabel="Change status"
        danger={status !== "Active"}
        loading={changeStatus.isPending}
        onCancel={() => setStatus(null)}
        onConfirm={() => status && changeStatus.mutate(status)}
      />

      <Modal
        open={granting}
        onClose={() => setGranting(false)}
        title="Grant course access"
        footer={
          <>
            <Button variant="secondary" onClick={() => setGranting(false)}>
              Cancel
            </Button>
            <Button disabled={!grantCourse} loading={grant.isPending} onClick={() => grant.mutate(undefined)}>
              Grant access
            </Button>
          </>
        }
      >
        <div className="space-y-4">
          <p className="text-sm text-fg-muted">Use for scholarships, offline payments or support fixes. The course&apos;s normal access period applies. This is recorded in the audit log.</p>
          <Field label="Course" htmlFor="grant-course" required>
            <CourseSelect id="grant-course" value={grantCourse} onChange={setGrantCourse} />
          </Field>
          <Field label="Reason" htmlFor="grant-reason">
            <Textarea id="grant-reason" value={grantReason} onChange={(e) => setGrantReason(e.target.value)} maxLength={500} />
          </Field>
        </div>
      </Modal>
    </>
  );
}
