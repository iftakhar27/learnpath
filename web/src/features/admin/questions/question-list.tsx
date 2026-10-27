"use client";

import Link from "next/link";
import { usePathname, useRouter, useSearchParams } from "next/navigation";
import { keepPreviousData, useQuery } from "@tanstack/react-query";
import { api, unwrap } from "@/lib/api/client";
import { DIFFICULTIES, PUBLISH_STATUSES, QUESTION_TYPES, type Difficulty, type PublishStatus, type QuestionType } from "@/lib/api/types";
import { formatDateTime, humanize, truncate } from "@/lib/format";
import { LinkButton } from "@/components/ui/button";
import { Badge, EmptyState, ErrorState, SkeletonRows, StatusBadge } from "@/components/ui/feedback";
import { Field, Select } from "@/components/ui/field";
import { SearchInput } from "@/components/ui/search-input";
import { Card, PageHeader, Pagination, Table, Td, Th } from "@/components/ui/layout";
import { TopicPicker } from "../shared";

const PAGE_SIZE = 25;

export function QuestionList() {
  const params = useSearchParams();
  const router = useRouter();
  const pathname = usePathname();
  const get = (k: string) => params.get(k) ?? "";
  const filters = {
    courseId: get("courseId"),
    chapterId: get("chapterId"),
    topicId: get("topicId"),
    status: get("status") as PublishStatus | "",
    difficulty: get("difficulty") as Difficulty | "",
    type: get("type") as QuestionType | "",
    q: get("q"),
    page: Number(get("page")) || 1,
  };

  /** Filters live in the URL so links from the syllabus work and the back button restores the view. */
  const update = (patch: Partial<Record<keyof typeof filters, string | number>>) => {
    const next = new URLSearchParams(params.toString());
    for (const [k, v] of Object.entries({ page: 1, ...patch })) {
      if (v === "" || v === undefined || (k === "page" && v === 1)) next.delete(k);
      else next.set(k, String(v));
    }
    router.replace(`${pathname}?${next.toString()}`, { scroll: false });
  };

  const query = useQuery({
    queryKey: ["admin", "questions", filters],
    placeholderData: keepPreviousData,
    queryFn: () =>
      unwrap(
        api.GET("/api/v1/admin/questions", {
          params: {
            query: {
              courseId: filters.courseId || undefined,
              chapterId: filters.chapterId || undefined,
              topicId: filters.topicId || undefined,
              status: filters.status || undefined,
              difficulty: filters.difficulty || undefined,
              type: filters.type || undefined,
              q: filters.q || undefined,
              page: filters.page,
              pageSize: PAGE_SIZE,
            },
          },
        }),
      ),
  });

  const newHref = `/admin/questions/new?${new URLSearchParams(Object.entries({ courseId: filters.courseId, chapterId: filters.chapterId, topicId: filters.topicId }).filter(([, v]) => v)).toString()}`;

  return (
    <>
      <PageHeader
        title="Question bank"
        description="Questions are tagged to a topic and reused in practice and tests. New and imported questions start as drafts."
        actions={
          <>
            <LinkButton variant="secondary" href="/admin/questions/import">
              Import CSV
            </LinkButton>
            <LinkButton href={newHref}>New question</LinkButton>
          </>
        }
      />

      <Card className="mb-4 space-y-3 p-4">
        <TopicPicker
          value={{ courseId: filters.courseId, chapterId: filters.chapterId, topicId: filters.topicId }}
          onChange={(v) => update({ courseId: v.courseId, chapterId: v.chapterId, topicId: v.topicId })}
          idPrefix="qf"
        />
        <div className="grid gap-3 sm:grid-cols-2 lg:grid-cols-4">
          <Field label="Status" htmlFor="qf-status">
            <Select id="qf-status" value={filters.status} onChange={(e) => update({ status: e.target.value })}>
              <option value="">Any status</option>
              {PUBLISH_STATUSES.map((s) => (
                <option key={s}>{s}</option>
              ))}
            </Select>
          </Field>
          <Field label="Type" htmlFor="qf-type">
            <Select id="qf-type" value={filters.type} onChange={(e) => update({ type: e.target.value })}>
              <option value="">Any type</option>
              {QUESTION_TYPES.map((t) => (
                <option key={t} value={t}>
                  {humanize(t)}
                </option>
              ))}
            </Select>
          </Field>
          <Field label="Difficulty" htmlFor="qf-diff">
            <Select id="qf-diff" value={filters.difficulty} onChange={(e) => update({ difficulty: e.target.value })}>
              <option value="">Any difficulty</option>
              {DIFFICULTIES.map((d) => (
                <option key={d}>{d}</option>
              ))}
            </Select>
          </Field>
          <SearchInput key={filters.q} id="qf-q" label="Search" placeholder="Text or exact reference" value={filters.q} onSearch={(q) => update({ q })} />
        </div>
      </Card>

      <Card>
        {query.isPending ? (
          <SkeletonRows />
        ) : query.isError ? (
          <ErrorState error={query.error} onRetry={() => query.refetch()} />
        ) : query.data.items.length === 0 ? (
          <EmptyState title="No questions match" action={<LinkButton href={newHref}>Add a question</LinkButton>}>
            Change the filters, add a question, or import a CSV file.
          </EmptyState>
        ) : (
          <>
            <Table>
              <thead>
                <tr>
                  <Th>Question</Th>
                  <Th>Topic</Th>
                  <Th>Type</Th>
                  <Th>Status</Th>
                  <Th>Updated</Th>
                </tr>
              </thead>
              <tbody className={query.isPlaceholderData ? "opacity-60" : ""}>
                {query.data.items.map((q) => (
                  <tr key={q.id} className="hover:bg-surface-muted/50">
                    <Td className="max-w-md">
                      <Link href={`/admin/questions/${q.id}`} className="text-primary hover:underline">
                        {truncate(q.stemEn, 110)}
                      </Link>
                      <div className="mt-0.5 flex flex-wrap gap-2 text-xs text-fg-muted">
                        {q.externalRef && <span className="font-mono">{q.externalRef}</span>}
                        <span>v{q.version}</span>
                        {q.openReports > 0 && <Badge tone="danger">{q.openReports} report{q.openReports > 1 ? "s" : ""}</Badge>}
                      </div>
                    </Td>
                    <Td>
                      {q.topic}
                      <div className="text-xs text-fg-muted">{q.chapter}</div>
                    </Td>
                    <Td className="whitespace-nowrap">
                      {humanize(q.type)}
                      <div className="text-xs text-fg-muted">{q.difficulty}</div>
                    </Td>
                    <Td>
                      <StatusBadge status={q.status} />
                    </Td>
                    <Td className="whitespace-nowrap text-fg-muted">{formatDateTime(q.updatedAt)}</Td>
                  </tr>
                ))}
              </tbody>
            </Table>
            <Pagination page={query.data.page} pageSize={query.data.pageSize} total={query.data.total} onPage={(page) => update({ page })} />
          </>
        )}
      </Card>
    </>
  );
}
