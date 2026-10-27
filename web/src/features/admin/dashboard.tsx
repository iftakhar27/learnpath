"use client";

import Link from "next/link";
import { useQuery } from "@tanstack/react-query";
import { api, unwrap } from "@/lib/api/client";
import { formatNumber, formatPaise } from "@/lib/format";
import { can, useSession } from "@/lib/session";
import { Card, PageHeader, StatTile, Table, Td, Th } from "@/components/ui/layout";
import { EmptyState, ErrorState, PageLoading } from "@/components/ui/feedback";

export function Dashboard() {
  const { data: user } = useSession();
  const q = useQuery({ queryKey: ["admin", "dashboard"], queryFn: () => unwrap(api.GET("/api/v1/admin/dashboard")) });

  if (q.isPending) return <PageLoading />;
  if (q.isError) return <ErrorState error={q.error} onRetry={() => q.refetch()} />;
  const d = q.data;

  return (
    <>
      <PageHeader title="Dashboard" description="Figures come straight from recorded activity. Revenue is GST-inclusive." />

      <div className="grid gap-4 sm:grid-cols-2 xl:grid-cols-4">
        <StatTile label="Registered students" value={formatNumber(d.totalStudents)} />
        <StatTile label="Active learners" value={formatNumber(d.activeStudents7d)} hint={`${formatNumber(d.activeStudents30d)} in the last 30 days`} />
        <StatTile label="Enrollments" value={formatNumber(d.enrollments)} />
        <StatTile label="Test attempts (30 days)" value={formatNumber(d.testAttempts30d)} hint={d.averageTestPercentage30d == null ? "No submitted attempts yet" : `Average score ${d.averageTestPercentage30d}%`} />
        <StatTile label="Paid orders (30 days)" value={formatNumber(d.paidOrders30d)} hint={`${formatPaise(d.revenue30dInPaise)} revenue`} />
        <StatTile label="Failed payments (30 days)" value={formatNumber(d.failedOrders30d)} />
        <StatTile
          label="Open support tickets"
          value={formatNumber(d.openTickets)}
          hint={can.support(user?.roles) && d.openTickets > 0 ? <Link className="text-primary hover:underline" href="/admin/support">Review tickets</Link> : undefined}
        />
        <StatTile
          label="Reported questions"
          value={formatNumber(d.openQuestionReports)}
          hint={d.failedImportRows30d > 0 ? `${formatNumber(d.failedImportRows30d)} failed import rows in 30 days` : undefined}
        />
      </div>

      <Card title="Most enrolled courses" className="mt-6">
        {d.topCourses.length === 0 ? (
          <EmptyState title="No enrollments yet">Courses appear here once students enrol.</EmptyState>
        ) : (
          <Table>
            <thead>
              <tr>
                <Th>Course</Th>
                <Th className="text-right">Enrollments</Th>
              </tr>
            </thead>
            <tbody>
              {d.topCourses.map((c) => (
                <tr key={c.courseId}>
                  <Td>{can.editContent(user?.roles) ? <Link className="text-primary hover:underline" href={`/admin/catalog/${c.courseId}`}>{c.title}</Link> : c.title}</Td>
                  <Td className="text-right tabular-nums">{formatNumber(c.enrollments)}</Td>
                </tr>
              ))}
            </tbody>
          </Table>
        )}
      </Card>
    </>
  );
}
