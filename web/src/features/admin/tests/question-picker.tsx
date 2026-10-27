"use client";

import { keepPreviousData, useQuery } from "@tanstack/react-query";
import { useState } from "react";
import { api, unwrap } from "@/lib/api/client";
import { DIFFICULTIES, type AdminQuestionListItem, type Difficulty } from "@/lib/api/types";
import { humanize, truncate } from "@/lib/format";
import { Button } from "@/components/ui/button";
import { Modal } from "@/components/ui/dialog";
import { EmptyState, ErrorState, SkeletonRows, StatusBadge } from "@/components/ui/feedback";
import { Checkbox, Field, Input, Select } from "@/components/ui/field";
import { Pagination } from "@/components/ui/layout";
import { TopicPicker, type TopicSelection } from "../shared";

const PAGE_SIZE = 20;

/** Search the question bank and pick questions to add to a test. */
export function QuestionPicker({
  open,
  onClose,
  onAdd,
  excludeIds,
  initialCourseId,
}: {
  open: boolean;
  onClose: () => void;
  onAdd: (items: AdminQuestionListItem[]) => void;
  excludeIds: Set<string>;
  initialCourseId: string;
}) {
  const [topic, setTopic] = useState<TopicSelection>({ courseId: initialCourseId, chapterId: "", topicId: "" });
  const [difficulty, setDifficulty] = useState<Difficulty | "">("");
  const [includeDrafts, setIncludeDrafts] = useState(false);
  const [search, setSearch] = useState("");
  const [q, setQ] = useState("");
  const [page, setPage] = useState(1);
  const [selected, setSelected] = useState<Map<string, AdminQuestionListItem>>(new Map());

  const filters = { ...topic, difficulty, includeDrafts, q, page };
  const query = useQuery({
    queryKey: ["admin", "questions", "picker", filters],
    enabled: open,
    placeholderData: keepPreviousData,
    queryFn: () =>
      unwrap(
        api.GET("/api/v1/admin/questions", {
          params: {
            query: {
              courseId: topic.courseId || undefined,
              chapterId: topic.chapterId || undefined,
              topicId: topic.topicId || undefined,
              difficulty: difficulty || undefined,
              status: includeDrafts ? undefined : "Published",
              q: q || undefined,
              page,
              pageSize: PAGE_SIZE,
            },
          },
        }),
      ),
  });

  const toggle = (item: AdminQuestionListItem, on: boolean) =>
    setSelected((prev) => {
      const next = new Map(prev);
      if (on) next.set(item.id, item);
      else next.delete(item.id);
      return next;
    });

  const close = () => {
    setSelected(new Map());
    onClose();
  };

  return (
    <Modal
      open={open}
      onClose={close}
      title="Add questions from the bank"
      wide
      footer={
        <>
          <Button variant="secondary" onClick={close}>
            Cancel
          </Button>
          <Button
            disabled={selected.size === 0}
            onClick={() => {
              onAdd([...selected.values()]);
              close();
            }}
          >
            Add {selected.size || ""} question{selected.size === 1 ? "" : "s"}
          </Button>
        </>
      }
    >
      <div className="space-y-3">
        <TopicPicker value={topic} onChange={(v) => { setTopic(v); setPage(1); }} idPrefix="qp" />
        <div className="grid gap-3 sm:grid-cols-[1fr_10rem]">
          <form
            onSubmit={(e) => {
              e.preventDefault();
              setQ(search.trim());
              setPage(1);
            }}
          >
            <Field label="Search" htmlFor="qp-q">
              <Input id="qp-q" type="search" value={search} onChange={(e) => setSearch(e.target.value)} placeholder="Question text or reference, then Enter" />
            </Field>
          </form>
          <Field label="Difficulty" htmlFor="qp-diff">
            <Select id="qp-diff" value={difficulty} onChange={(e) => { setDifficulty(e.target.value as Difficulty | ""); setPage(1); }}>
              <option value="">Any</option>
              {DIFFICULTIES.map((d) => (
                <option key={d}>{d}</option>
              ))}
            </Select>
          </Field>
        </div>
        <Checkbox label="Include draft questions" hint="A test can only be published once all its questions are published." checked={includeDrafts} onChange={(e) => { setIncludeDrafts(e.target.checked); setPage(1); }} />

        <div className="rounded-lg border border-border">
          {query.isPending ? (
            <SkeletonRows rows={4} />
          ) : query.isError ? (
            <ErrorState error={query.error} onRetry={() => query.refetch()} />
          ) : query.data.items.length === 0 ? (
            <EmptyState title="No questions found">Try another topic or include drafts.</EmptyState>
          ) : (
            <>
              <ul className="divide-y divide-border">
                {query.data.items.map((item) => {
                  const added = excludeIds.has(item.id);
                  return (
                    <li key={item.id} className="flex items-start gap-3 px-3 py-2.5 text-sm">
                      <input
                        type="checkbox"
                        className="mt-1 h-4 w-4 accent-[var(--primary)]"
                        aria-label={`Select: ${truncate(item.stemEn, 60)}`}
                        disabled={added}
                        checked={added || selected.has(item.id)}
                        onChange={(e) => toggle(item, e.target.checked)}
                      />
                      <div className="min-w-0 flex-1">
                        <p>{truncate(item.stemEn, 160)}</p>
                        <p className="mt-0.5 flex flex-wrap items-center gap-2 text-xs text-fg-muted">
                          {item.topic} · {humanize(item.type)} · {item.difficulty}
                          {item.externalRef && <span className="font-mono">{item.externalRef}</span>}
                          {item.status !== "Published" && <StatusBadge status={item.status} />}
                          {added && <span>Already in test</span>}
                        </p>
                      </div>
                    </li>
                  );
                })}
              </ul>
              <Pagination page={query.data.page} pageSize={query.data.pageSize} total={query.data.total} onPage={setPage} />
            </>
          )}
        </div>
      </div>
    </Modal>
  );
}
