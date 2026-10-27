"use client";

import Link from "next/link";
import { usePathname, useRouter, useSearchParams } from "next/navigation";
import { useQuery } from "@tanstack/react-query";
import { api, unwrap } from "@/lib/api/client";
import { PUBLISH_STATUSES, type PublishStatus } from "@/lib/api/types";
import { formatDateTime, formatNumber, humanize } from "@/lib/format";
import { LinkButton } from "@/components/ui/button";
import { Badge, EmptyState, ErrorState, SkeletonRows, StatusBadge } from "@/components/ui/feedback";
import { Select } from "@/components/ui/field";
import { Card, PageHeader, Table, Td, Th } from "@/components/ui/layout";
import { CourseSelect } from "../shared";

export function TestList() {
  const params = useSearchParams();
  const router = useRouter();
  const pathname = usePathname();
  const courseId = params.get("courseId") ?? "";
  const status = (params.get("status") ?? "") as PublishStatus | "";
  const setParam = (k: string, v: string) => {
    const next = new URLSearchParams(params.toString());
    if (v) next.set(k, v);
    else next.delete(k);
    router.replace(`${pathname}?${next}`, { scroll: false });
  };

  const tests = useQuery({
    queryKey: ["admin", "tests", { courseId, status }],
    queryFn: () => unwrap(api.GET("/api/v1/admin/tests", { params: { query: { courseId: courseId || undefined, status: status || undefined } } })),
  });

  return (
    <>
      <PageHeader
        title="Tests"
        description="Build chapter tests, mock exams and quizzes from the question bank. Timing and scoring are enforced by the server."
        actions={<LinkButton href={`/admin/tests/new${courseId ? `?courseId=${courseId}` : ""}`}>New test</LinkButton>}
      />
      <Card
        actions={
          <div className="flex flex-wrap gap-2">
            <div className="w-56">
              <CourseSelect value={courseId} onChange={(v) => setParam("courseId", v)} allowEmpty="All courses" />
            </div>
            <Select aria-label="Filter by status" value={status} onChange={(e) => setParam("status", e.target.value)} className="w-40">
              <option value="">Any status</option>
              {PUBLISH_STATUSES.map((s) => (
                <option key={s}>{s}</option>
              ))}
            </Select>
          </div>
        }
        title="All tests"
      >
        {tests.isPending ? (
          <SkeletonRows />
        ) : tests.isError ? (
          <ErrorState error={tests.error} onRetry={() => tests.refetch()} />
        ) : tests.data.length === 0 ? (
          <EmptyState title="No tests yet" action={<LinkButton href="/admin/tests/new">Create a test</LinkButton>}>
            Add questions to the question bank first, then build a test from them.
          </EmptyState>
        ) : (
          <Table>
            <thead>
              <tr>
                <Th>Test</Th>
                <Th>Course</Th>
                <Th className="text-right">Questions</Th>
                <Th className="text-right">Marks</Th>
                <Th className="text-right">Minutes</Th>
                <Th>Status</Th>
                <Th className="text-right">Attempts</Th>
              </tr>
            </thead>
            <tbody>
              {tests.data.map((t) => (
                <tr key={t.id} className="hover:bg-surface-muted/50">
                  <Td>
                    <Link href={`/admin/tests/${t.id}`} className="font-medium text-primary hover:underline">
                      {t.titleEn}
                    </Link>
                    <div className="mt-0.5 flex gap-2 text-xs text-fg-muted">
                      {humanize(t.type)} · created {formatDateTime(t.createdAt)} {t.isFree && <Badge tone="primary">Free</Badge>}
                    </div>
                  </Td>
                  <Td>{t.courseTitle ?? <span className="text-fg-muted">—</span>}</Td>
                  <Td className="text-right tabular-nums">{t.questionCount}</Td>
                  <Td className="text-right tabular-nums">{formatNumber(t.maxMarks)}</Td>
                  <Td className="text-right tabular-nums">{t.durationMinutes}</Td>
                  <Td>
                    <StatusBadge status={t.status} />
                  </Td>
                  <Td className="text-right tabular-nums">{formatNumber(t.attempts)}</Td>
                </tr>
              ))}
            </tbody>
          </Table>
        )}
      </Card>
    </>
  );
}
