"use client";

import Link from "next/link";
import { useMutation, useQuery, useQueryClient, type QueryKey } from "@tanstack/react-query";
import { useState } from "react";
import { api, errorMessage, unwrap } from "@/lib/api/client";
import type { AdminCourseTree, PublishStatus } from "@/lib/api/types";
import { useToast } from "@/components/ui/toast";
import { Button } from "@/components/ui/button";
import { ConfirmDialog } from "@/components/ui/dialog";
import { Field, Select } from "@/components/ui/field";

// ---------- Queries shared across admin screens ----------

export const useExams = () =>
  useQuery({ queryKey: ["admin", "exams"], queryFn: () => unwrap(api.GET("/api/v1/admin/exams")), staleTime: 5 * 60_000 });

export const useCourses = () =>
  useQuery({ queryKey: ["admin", "courses"], queryFn: () => unwrap(api.GET("/api/v1/admin/courses", { params: { query: {} } })) });

export const useCourseTree = (courseId: string | null | undefined) =>
  useQuery({
    queryKey: ["admin", "course-tree", courseId],
    queryFn: () => unwrap(api.GET("/api/v1/admin/courses/{id}/tree", { params: { path: { id: courseId! } } })),
    enabled: !!courseId,
  });

/** Runs a mutation, invalidates the given query prefixes, and reports the outcome in a toast. */
export function useAction<TVars, TResult = unknown>(
  fn: (vars: TVars) => Promise<TResult>,
  opts: { invalidate?: QueryKey[]; success?: string | ((r: TResult) => string); onSuccess?: (r: TResult, v: TVars) => void } = {},
) {
  const qc = useQueryClient();
  const toast = useToast();
  return useMutation({
    mutationFn: fn,
    onSuccess: async (result, vars) => {
      await Promise.all((opts.invalidate ?? []).map((k) => qc.invalidateQueries({ queryKey: k })));
      const msg = typeof opts.success === "function" ? opts.success(result) : opts.success;
      if (msg) toast(msg);
      opts.onSuccess?.(result, vars);
    },
    onError: (e) => toast(errorMessage(e), "error"),
  });
}

// ---------- Pickers ----------

export function ExamSelect({ value, onChange, id, allowEmpty, required }: { value: string; onChange: (v: string) => void; id?: string; allowEmpty?: string; required?: boolean }) {
  const exams = useExams();
  return (
    <Select id={id} value={value} onChange={(e) => onChange(e.target.value)} required={required} disabled={exams.isPending}>
      {allowEmpty !== undefined && <option value="">{allowEmpty}</option>}
      {!allowEmpty && !value && <option value="">Select exam…</option>}
      {exams.data?.map((e) => (
        <option key={e.id} value={e.id}>
          {e.nameEn}
        </option>
      ))}
    </Select>
  );
}

export function CourseSelect({ value, onChange, id, allowEmpty = "Select course…" }: { value: string; onChange: (v: string) => void; id?: string; allowEmpty?: string }) {
  const courses = useCourses();
  return (
    <Select id={id} value={value} onChange={(e) => onChange(e.target.value)} disabled={courses.isPending}>
      <option value="">{allowEmpty}</option>
      {courses.data?.map((c) => (
        <option key={c.id} value={c.id}>
          {c.titleEn}
          {c.status !== "Published" ? ` (${c.status})` : ""}
        </option>
      ))}
    </Select>
  );
}

export type TopicSelection = { courseId: string; chapterId: string; topicId: string };

export function chaptersOf(tree: AdminCourseTree | undefined) {
  return tree?.subjects.flatMap((s) => s.chapters.map((c) => ({ ...c, subjectTitle: s.titleEn }))) ?? [];
}

/** Course → chapter → topic cascade. `requireTopic` hides the "any" options (for tagging a question). */
export function TopicPicker({ value, onChange, requireTopic, idPrefix = "tp" }: { value: TopicSelection; onChange: (v: TopicSelection) => void; requireTopic?: boolean; idPrefix?: string }) {
  const tree = useCourseTree(value.courseId || null);
  const chapters = chaptersOf(tree.data);
  const topics = chapters.find((c) => c.id === value.chapterId)?.topics ?? [];
  return (
    <div className="grid gap-3 sm:grid-cols-3">
      <Field label="Course" htmlFor={`${idPrefix}-course`} required={requireTopic}>
        <CourseSelect id={`${idPrefix}-course`} value={value.courseId} onChange={(courseId) => onChange({ courseId, chapterId: "", topicId: "" })} allowEmpty={requireTopic ? "Select course…" : "All courses"} />
      </Field>
      <Field label="Chapter" htmlFor={`${idPrefix}-chapter`} required={requireTopic}>
        <Select id={`${idPrefix}-chapter`} value={value.chapterId} disabled={!value.courseId || tree.isPending} onChange={(e) => onChange({ ...value, chapterId: e.target.value, topicId: "" })}>
          <option value="">{requireTopic ? "Select chapter…" : "All chapters"}</option>
          {chapters.map((c) => (
            <option key={c.id} value={c.id}>
              {c.subjectTitle} · {c.titleEn}
            </option>
          ))}
        </Select>
      </Field>
      <Field label="Topic" htmlFor={`${idPrefix}-topic`} required={requireTopic}>
        <Select id={`${idPrefix}-topic`} value={value.topicId} disabled={!value.chapterId} onChange={(e) => onChange({ ...value, topicId: e.target.value })}>
          <option value="">{requireTopic ? "Select topic…" : "All topics"}</option>
          {topics.map((t) => (
            <option key={t.id} value={t.id}>
              {t.titleEn}
            </option>
          ))}
        </Select>
      </Field>
    </div>
  );
}

// ---------- Publishing controls ----------

/** Publish / unpublish / archive buttons. Only rendered for roles that can publish. */
export function StatusActions({ status, onChange, pending, noun }: { status: PublishStatus; onChange: (s: PublishStatus) => void; pending?: boolean; noun: string }) {
  const [confirm, setConfirm] = useState<PublishStatus | null>(null);
  const ask = (s: PublishStatus) => setConfirm(s);
  const messages: Record<PublishStatus, string> = {
    Published: `Students will be able to see this ${noun}.`,
    Draft: `Students will no longer see this ${noun}. Existing results are kept.`,
    Archived: `This ${noun} will be hidden and marked as retired. You can restore it to draft later.`,
  };
  return (
    <>
      {status !== "Published" && (
        <Button size="sm" onClick={() => ask("Published")} loading={pending && confirm === "Published"}>
          Publish
        </Button>
      )}
      {status === "Published" && (
        <Button size="sm" variant="secondary" onClick={() => ask("Draft")}>
          Unpublish
        </Button>
      )}
      {status === "Archived" ? (
        <Button size="sm" variant="secondary" onClick={() => ask("Draft")}>
          Restore to draft
        </Button>
      ) : (
        <Button size="sm" variant="ghost" onClick={() => ask("Archived")}>
          Archive
        </Button>
      )}
      <ConfirmDialog
        open={confirm !== null}
        title={confirm === "Published" ? `Publish ${noun}?` : confirm === "Archived" ? `Archive ${noun}?` : `Move ${noun} to draft?`}
        message={confirm ? messages[confirm] : ""}
        confirmLabel={confirm === "Published" ? "Publish" : confirm === "Archived" ? "Archive" : "Move to draft"}
        loading={pending}
        onCancel={() => setConfirm(null)}
        onConfirm={() => {
          if (confirm) onChange(confirm);
          setConfirm(null);
        }}
      />
    </>
  );
}

export function BackLink({ href, children }: { href: string; children: React.ReactNode }) {
  return (
    <Link href={href} className="inline-block text-sm text-fg-muted hover:text-fg">
      ← {children}
    </Link>
  );
}
