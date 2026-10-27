"use client";

import { useParams, useRouter, useSearchParams } from "next/navigation";
import { useState } from "react";
import { useForm } from "react-hook-form";
import { useQuery } from "@tanstack/react-query";
import { api, unwrap } from "@/lib/api/client";
import { TEST_TYPES, type AdminTest, type AdminTestQuestion, type PublishStatus, type SectionUpsert, type TestType, type TestUpsert } from "@/lib/api/types";
import { blankToNull, formatNumber, fromLocalInput, humanize, toLocalInput, truncate } from "@/lib/format";
import { can, useSession } from "@/lib/session";
import { Button } from "@/components/ui/button";
import { Badge, EmptyState, ErrorState, InlineError, PageLoading, StatusBadge } from "@/components/ui/feedback";
import { BilingualRow, Checkbox, Field, Input, Select, Textarea } from "@/components/ui/field";
import { Card, PageHeader } from "@/components/ui/layout";
import { QuestionPicker } from "./question-picker";
import { BackLink, CourseSelect, ExamSelect, StatusActions, useAction } from "../shared";

type Settings = {
  titleEn: string;
  titleHi: string;
  type: TestType;
  courseId: string;
  examId: string;
  durationMinutes: string;
  maxAttempts: string;
  isFree: boolean;
  shuffleQuestions: boolean;
  shuffleOptions: boolean;
  availableFrom: string;
  availableUntil: string;
  instructionsEn: string;
  instructionsHi: string;
};

/** One question row in the builder. Empty marks mean "use the question's default". */
type Row = Omit<AdminTestQuestion, "marks" | "negativeMarks"> & { marks: string; negativeMarks: string };

export function TestBuilderPage() {
  const { id } = useParams<{ id?: string }>();
  const isNew = !id;
  const q = useQuery({
    queryKey: ["admin", "test", id],
    queryFn: () => unwrap(api.GET("/api/v1/admin/tests/{id}", { params: { path: { id: id! } } })),
    enabled: !isNew,
  });
  if (!isNew && q.isPending) return <PageLoading />;
  if (!isNew && q.isError) return <ErrorState error={q.error} onRetry={() => q.refetch()} />;
  return <TestBuilder key={q.data?.id ?? "new"} test={q.data} />;
}

function TestBuilder({ test }: { test?: AdminTest }) {
  const router = useRouter();
  const params = useSearchParams();
  const { data: user } = useSession();
  const locked = !!test && test.attempts > 0;

  const { register, handleSubmit, watch, setValue, formState } = useForm<Settings>({
    defaultValues: {
      titleEn: test?.titleEn ?? "",
      titleHi: test?.titleHi ?? "",
      type: test?.type ?? "Chapter",
      courseId: test?.courseId ?? params.get("courseId") ?? "",
      examId: test?.examId ?? "",
      durationMinutes: String(test?.durationMinutes ?? 30),
      maxAttempts: test?.maxAttempts != null ? String(test.maxAttempts) : "",
      isFree: test?.isFree ?? false,
      shuffleQuestions: test?.shuffleQuestions ?? false,
      shuffleOptions: test?.shuffleOptions ?? false,
      availableFrom: toLocalInput(test?.availableFrom),
      availableUntil: toLocalInput(test?.availableUntil),
      instructionsEn: test?.instructionsEn ?? "",
      instructionsHi: test?.instructionsHi ?? "",
    },
  });
  const [sections, setSections] = useState<SectionUpsert[]>(test?.sections ?? []);
  const [rows, setRows] = useState<Row[]>(test?.questions.map((x) => ({ ...x, marks: String(x.marks), negativeMarks: String(x.negativeMarks) })) ?? []);
  const [picking, setPicking] = useState(false);
  const [formError, setFormError] = useState<string | null>(null);
  const s = watch();
  const errors = formState.errors;

  const invalidate = [["admin", "tests"], ["admin", "test"]];
  const save = useAction(
    (body: TestUpsert) => (test ? unwrap(api.PUT("/api/v1/admin/tests/{id}", { params: { path: { id: test.id } }, body })) : unwrap(api.POST("/api/v1/admin/tests", { body }))),
    { invalidate, success: test ? "Test saved" : "Test created as draft", onSuccess: (r) => !test && router.replace(`/admin/tests/${r.id}`) },
  );
  const setStatus = useAction((status: PublishStatus) => unwrap(api.POST("/api/v1/admin/tests/{id}/status", { params: { path: { id: test!.id } }, body: { status } })), {
    invalidate,
    success: "Test status updated",
  });
  const duplicate = useAction(() => unwrap(api.POST("/api/v1/admin/tests/{id}/duplicate", { params: { path: { id: test!.id } } })), {
    invalidate,
    success: "Copy created as a draft",
    onSuccess: (r) => router.push(`/admin/tests/${r.id}`),
  });

  const move = (i: number, dir: -1 | 1) =>
    setRows((prev) => {
      const next = [...prev];
      const j = i + dir;
      if (j < 0 || j >= next.length) return prev;
      [next[i], next[j]] = [next[j], next[i]];
      return next;
    });
  const patchRow = (i: number, patch: Partial<Row>) => setRows((prev) => prev.map((r, k) => (k === i ? { ...r, ...patch } : r)));
  const removeSection = (index: number) => {
    setSections((prev) => prev.filter((_, k) => k !== index));
    setRows((prev) => prev.map((r) => (r.sectionIndex == null ? r : r.sectionIndex === index ? { ...r, sectionIndex: null } : r.sectionIndex > index ? { ...r, sectionIndex: r.sectionIndex - 1 } : r)));
  };

  const explicitMarks = rows.filter((r) => r.marks !== "").reduce((sum, r) => sum + Number(r.marks), 0);
  const defaultCount = rows.filter((r) => r.marks === "").length;
  const negatives = [...new Set(rows.map((r) => r.negativeMarks).filter((x) => x !== ""))];

  const insertInstructions = () => {
    const neg = negatives.length === 1 ? `−${negatives[0]}` : "as marked on each question";
    const marks = rows.length && defaultCount === 0 ? ` Total ${formatNumber(explicitMarks)} marks.` : "";
    setValue(
      "instructionsEn",
      `${rows.length} questions · ${s.durationMinutes} minutes.${marks}\nCorrect answer: marks shown on each question. Wrong answer: ${neg}. Unanswered: 0.\nThe test submits automatically when time runs out. Your answers are saved as you go, so you can resume after a network drop.`,
    );
  };

  const submit = handleSubmit((v) => {
    setFormError(null);
    if (rows.length === 0) return setFormError("Add at least one question.");
    if (sections.some((x) => !x.titleEn.trim())) return setFormError("Give every section a name, or remove it.");
    const from = fromLocalInput(v.availableFrom);
    const until = fromLocalInput(v.availableUntil);
    if (from && until && until <= from) return setFormError("The closing time must be after the opening time.");
    save.mutate({
      courseId: v.courseId || null,
      examId: v.examId || null,
      type: v.type,
      titleEn: v.titleEn.trim(),
      titleHi: blankToNull(v.titleHi),
      instructionsEn: blankToNull(v.instructionsEn),
      instructionsHi: blankToNull(v.instructionsHi),
      durationMinutes: Number(v.durationMinutes),
      shuffleQuestions: v.shuffleQuestions,
      shuffleOptions: v.shuffleOptions,
      isFree: v.isFree,
      maxAttempts: v.maxAttempts ? Number(v.maxAttempts) : null,
      availableFrom: from,
      availableUntil: until,
      sections: sections.map((x) => ({ titleEn: x.titleEn.trim(), titleHi: blankToNull(x.titleHi ?? "") })),
      questions: rows.map((r) => ({
        questionId: r.questionId,
        sectionIndex: r.sectionIndex,
        marks: r.marks === "" ? null : Number(r.marks),
        negativeMarks: r.negativeMarks === "" ? null : Number(r.negativeMarks),
      })),
    });
  });

  const drafts = rows.filter((r) => r.questionStatus !== "Published").length;

  return (
    <>
      <PageHeader
        back={<BackLink href="/admin/tests">Tests</BackLink>}
        title={
          test ? (
            <span className="flex flex-wrap items-center gap-2">
              {test.titleEn} <StatusBadge status={test.status} />
            </span>
          ) : (
            "New test"
          )
        }
        description={test ? `${formatNumber(test.attempts)} attempt${test.attempts === 1 ? "" : "s"} so far` : "Saved as a draft until you publish it."}
        actions={
          test && (
            <>
              <Button size="sm" variant="secondary" loading={duplicate.isPending} onClick={() => duplicate.mutate(undefined)}>
                Duplicate
              </Button>
              {can.publishContent(user?.roles) && <StatusActions status={test.status} noun="test" pending={setStatus.isPending} onChange={(x) => setStatus.mutate(x)} />}
            </>
          )
        }
      />

      {locked && (
        <p className="mb-4 rounded-lg bg-warning-soft px-3 py-2 text-sm text-warning">
          Students have already taken this test, so its questions and settings are locked to keep results comparable. Use <strong>Duplicate</strong> to make a
          changed version.
        </p>
      )}
      {!locked && drafts > 0 && (
        <p className="mb-4 rounded-lg bg-warning-soft px-3 py-2 text-sm text-warning">
          {drafts} question{drafts === 1 ? " is" : "s are"} still in draft. Publish {drafts === 1 ? "it" : "them"} before publishing this test.
        </p>
      )}

      <form onSubmit={submit} noValidate>
        <fieldset disabled={locked} className="space-y-6">
          <Card title="Settings" className="p-0">
            <div className="space-y-4 p-4">
              <BilingualRow>
                <Field label="Title (English)" htmlFor="tb-title" required error={errors.titleEn?.message}>
                  <Input id="tb-title" {...register("titleEn", { required: "Title is required", maxLength: { value: 200, message: "Max 200 characters" } })} />
                </Field>
                <Field label="Title (Hindi)" htmlFor="tb-title-hi">
                  <Input id="tb-title-hi" lang="hi" {...register("titleHi")} />
                </Field>
              </BilingualRow>
              <div className="grid gap-4 sm:grid-cols-2 lg:grid-cols-4">
                <Field label="Type" htmlFor="tb-type">
                  <Select id="tb-type" {...register("type")}>
                    {TEST_TYPES.map((t) => (
                      <option key={t} value={t}>
                        {humanize(t)}
                      </option>
                    ))}
                  </Select>
                </Field>
                <Field label="Duration (minutes)" htmlFor="tb-dur" required error={errors.durationMinutes?.message}>
                  <Input id="tb-dur" type="number" min={1} max={600} {...register("durationMinutes", { validate: (x) => (Number(x) >= 1 && Number(x) <= 600) || "Between 1 and 600" })} />
                </Field>
                <Field label="Max attempts" htmlFor="tb-max" hint="Empty = unlimited" error={errors.maxAttempts?.message}>
                  <Input id="tb-max" type="number" min={1} max={100} {...register("maxAttempts", { validate: (x) => !x || (Number(x) >= 1 && Number(x) <= 100) || "Between 1 and 100" })} />
                </Field>
                <div className="space-y-2 pt-6">
                  <Checkbox label="Free test" hint="No purchase needed" {...register("isFree")} />
                </div>
              </div>
              <div className="grid gap-4 sm:grid-cols-2">
                <Field label="Course" htmlFor="tb-course" hint={s.isFree ? "Optional for free tests." : "Students need access to this course."}>
                  <CourseSelect id="tb-course" value={s.courseId} onChange={(v) => setValue("courseId", v)} allowEmpty="No course" />
                </Field>
                <Field label="Exam" htmlFor="tb-exam">
                  <ExamSelect id="tb-exam" value={s.examId} onChange={(v) => setValue("examId", v)} allowEmpty="Not exam-specific" />
                </Field>
              </div>
              {!s.isFree && !s.courseId && <p className="text-sm text-warning">A paid test needs a course, otherwise no student can unlock it.</p>}
              <div className="grid gap-4 sm:grid-cols-2">
                <Field label="Opens at" htmlFor="tb-from" hint="Optional, your local time.">
                  <Input id="tb-from" type="datetime-local" {...register("availableFrom")} />
                </Field>
                <Field label="Closes at" htmlFor="tb-until" hint="Optional.">
                  <Input id="tb-until" type="datetime-local" {...register("availableUntil")} />
                </Field>
              </div>
              <div className="flex flex-wrap gap-6">
                <Checkbox label="Shuffle question order" hint="Each attempt gets its own fixed order." {...register("shuffleQuestions")} />
                <Checkbox label="Shuffle options" hint="True/false options are never shuffled." {...register("shuffleOptions")} />
              </div>
              <BilingualRow>
                <Field label="Instructions (English)" htmlFor="tb-ins" hint={<button type="button" className="text-primary hover:underline" onClick={insertInstructions}>Insert standard instructions</button>}>
                  <Textarea id="tb-ins" rows={4} {...register("instructionsEn")} />
                </Field>
                <Field label="Instructions (Hindi)" htmlFor="tb-ins-hi">
                  <Textarea id="tb-ins-hi" rows={4} lang="hi" {...register("instructionsHi")} />
                </Field>
              </BilingualRow>
            </div>
          </Card>

          <Card
            title="Sections"
            actions={
              <Button size="sm" variant="secondary" onClick={() => setSections((p) => [...p, { titleEn: "", titleHi: null }])}>
                Add section
              </Button>
            }
          >
            {sections.length === 0 ? (
              <p className="px-4 py-3 text-sm text-fg-muted">Optional. Use sections for subject-wise papers, e.g. Physics / Chemistry / Biology.</p>
            ) : (
              <ul className="divide-y divide-border">
                {sections.map((sec, i) => (
                  <li key={i} className="grid gap-3 p-3 sm:grid-cols-[1fr_1fr_auto]">
                    <Input aria-label={`Section ${i + 1} name (English)`} placeholder={`Section ${i + 1} name`} value={sec.titleEn} onChange={(e) => setSections((p) => p.map((x, k) => (k === i ? { ...x, titleEn: e.target.value } : x)))} />
                    <Input aria-label={`Section ${i + 1} name (Hindi)`} placeholder="Hindi name (optional)" lang="hi" value={sec.titleHi ?? ""} onChange={(e) => setSections((p) => p.map((x, k) => (k === i ? { ...x, titleHi: e.target.value } : x)))} />
                    <Button size="sm" variant="ghost" onClick={() => removeSection(i)}>
                      Remove
                    </Button>
                  </li>
                ))}
              </ul>
            )}
          </Card>

          <Card
            title={
              <span>
                Questions <span className="font-normal text-fg-muted">· {rows.length} · {formatNumber(explicitMarks)} marks{defaultCount > 0 && ` + ${defaultCount} at default marks`}</span>
              </span>
            }
            actions={<Button size="sm" onClick={() => setPicking(true)}>Add questions</Button>}
          >
            {rows.length === 0 ? (
              <EmptyState title="No questions yet" action={<Button onClick={() => setPicking(true)}>Add questions</Button>} />
            ) : (
              <ol className="divide-y divide-border">
                {rows.map((r, i) => (
                  <li key={r.questionId} className="grid gap-3 p-3 lg:grid-cols-[2rem_1fr_auto] lg:items-start">
                    <span className="pt-2 text-sm font-semibold tabular-nums text-fg-muted">{i + 1}.</span>
                    <div className="min-w-0">
                      <p className="text-sm">{truncate(r.stemEn, 200)}</p>
                      <p className="mt-0.5 flex flex-wrap items-center gap-2 text-xs text-fg-muted">
                        {r.topic} · {humanize(r.type)} · {r.difficulty}
                        {r.externalRef && <span className="font-mono">{r.externalRef}</span>}
                        {r.questionStatus !== "Published" && <Badge tone="warning">{r.questionStatus}</Badge>}
                      </p>
                    </div>
                    <div className="flex flex-wrap items-end gap-2">
                      {sections.length > 0 && (
                        <label className="text-xs text-fg-muted">
                          Section
                          <Select className="mt-0.5 h-8 w-36" value={r.sectionIndex ?? ""} onChange={(e) => patchRow(i, { sectionIndex: e.target.value === "" ? null : Number(e.target.value) })}>
                            <option value="">None</option>
                            {sections.map((x, k) => (
                              <option key={k} value={k}>
                                {x.titleEn || `Section ${k + 1}`}
                              </option>
                            ))}
                          </Select>
                        </label>
                      )}
                      <label className="text-xs text-fg-muted">
                        Marks
                        <Input className="mt-0.5 h-8 w-20" type="number" step="0.25" min={0} placeholder="Default" value={r.marks} onChange={(e) => patchRow(i, { marks: e.target.value })} />
                      </label>
                      <label className="text-xs text-fg-muted">
                        Wrong (−)
                        <Input className="mt-0.5 h-8 w-20" type="number" step="0.25" min={0} placeholder="Default" value={r.negativeMarks} onChange={(e) => patchRow(i, { negativeMarks: e.target.value })} />
                      </label>
                      <span className="inline-flex pb-0.5">
                        <button type="button" aria-label={`Move question ${i + 1} up`} disabled={i === 0} onClick={() => move(i, -1)} className="rounded px-1.5 py-1 text-fg-muted hover:bg-surface-muted disabled:opacity-30">
                          ↑
                        </button>
                        <button type="button" aria-label={`Move question ${i + 1} down`} disabled={i === rows.length - 1} onClick={() => move(i, 1)} className="rounded px-1.5 py-1 text-fg-muted hover:bg-surface-muted disabled:opacity-30">
                          ↓
                        </button>
                      </span>
                      <Button size="sm" variant="ghost" onClick={() => setRows((p) => p.filter((_, k) => k !== i))} aria-label={`Remove question ${i + 1}`}>
                        Remove
                      </Button>
                    </div>
                  </li>
                ))}
              </ol>
            )}
          </Card>

          <InlineError error={formError} />
          {!locked && (
            <div className="flex justify-end">
              <Button type="submit" loading={save.isPending}>
                {test ? "Save changes" : "Create test"}
              </Button>
            </div>
          )}
        </fieldset>
      </form>

      <QuestionPicker
        open={picking}
        onClose={() => setPicking(false)}
        initialCourseId={s.courseId}
        excludeIds={new Set(rows.map((r) => r.questionId))}
        onAdd={(items) =>
          setRows((prev) => [
            ...prev,
            ...items.map((x) => ({
              questionId: x.id,
              sectionIndex: null,
              marks: "",
              negativeMarks: "",
              stemEn: x.stemEn,
              externalRef: x.externalRef,
              type: x.type,
              difficulty: x.difficulty,
              questionStatus: x.status,
              topic: x.topic,
            })),
          ])
        }
      />
    </>
  );
}
