"use client";

import { useParams, useRouter, useSearchParams } from "next/navigation";
import { useState } from "react";
import { useFieldArray, useForm } from "react-hook-form";
import { useQuery } from "@tanstack/react-query";
import { api, unwrap } from "@/lib/api/client";
import { DIFFICULTIES, QUESTION_TYPES, type AdminQuestion, type Difficulty, type PublishStatus, type QuestionType, type QuestionUpsert } from "@/lib/api/types";
import { blankToNull, humanize } from "@/lib/format";
import { can, useSession } from "@/lib/session";
import { Button } from "@/components/ui/button";
import { ErrorState, InlineError, PageLoading, StatusBadge } from "@/components/ui/feedback";
import { BilingualRow, Checkbox, Field, Input, Select, Textarea } from "@/components/ui/field";
import { Card, PageHeader } from "@/components/ui/layout";
import { BackLink, StatusActions, TopicPicker, useAction, type TopicSelection } from "../shared";

type Option = { textEn: string; textHi: string; isCorrect: boolean };
type Values = {
  type: QuestionType;
  difficulty: Difficulty;
  stemEn: string;
  stemHi: string;
  imageUrl: string;
  options: Option[];
  numericAnswer: string;
  numericTolerance: string;
  defaultMarks: string;
  defaultNegativeMarks: string;
  allowPartialMarking: boolean;
  explanationEn: string;
  explanationHi: string;
  hintEn: string;
  videoSolutionId: string;
  previousYear: string;
  externalRef: string;
};

const isOptionType = (t: QuestionType) => t === "SingleCorrect" || t === "MultipleCorrect" || t === "TrueFalse";
const TRUE_FALSE: Option[] = [
  { textEn: "True", textHi: "सत्य", isCorrect: false },
  { textEn: "False", textHi: "असत्य", isCorrect: false },
];
const EMPTY_OPTIONS: Option[] = Array.from({ length: 4 }, () => ({ textEn: "", textHi: "", isCorrect: false }));

function toValues(q?: AdminQuestion): Values {
  return {
    type: q?.type ?? "SingleCorrect",
    difficulty: q?.difficulty ?? "Medium",
    stemEn: q?.stemEn ?? "",
    stemHi: q?.stemHi ?? "",
    imageUrl: q?.imageUrl ?? "",
    options: q ? q.options.map((o) => ({ textEn: o.textEn, textHi: o.textHi ?? "", isCorrect: o.isCorrect })) : EMPTY_OPTIONS,
    numericAnswer: q?.numericAnswer != null ? String(q.numericAnswer) : "",
    numericTolerance: q ? String(q.numericTolerance) : "0",
    defaultMarks: q ? String(q.defaultMarks) : "4",
    defaultNegativeMarks: q ? String(q.defaultNegativeMarks) : "1",
    allowPartialMarking: q?.allowPartialMarking ?? false,
    explanationEn: q?.explanationEn ?? "",
    explanationHi: q?.explanationHi ?? "",
    hintEn: q?.hintEn ?? "",
    videoSolutionId: q?.videoSolutionId ?? "",
    previousYear: q?.previousYear != null ? String(q.previousYear) : "",
    externalRef: q?.externalRef ?? "",
  };
}

/** Mirrors the API's shape rules so mistakes show before saving. */
function shapeError(v: Values): string | null {
  if (!isOptionType(v.type)) {
    if (v.numericAnswer.trim() === "" || Number.isNaN(Number(v.numericAnswer))) return "Enter the numeric answer.";
    if (v.type === "Integer" && !Number.isInteger(Number(v.numericAnswer))) return "Integer questions need a whole-number answer.";
    return null;
  }
  const opts = v.options.filter((o) => o.textEn.trim());
  if (opts.length !== v.options.length) return "Every option needs English text (remove empty options).";
  const correct = opts.filter((o) => o.isCorrect).length;
  if (v.type === "TrueFalse") return opts.length === 2 && correct === 1 ? null : "Mark either True or False as correct.";
  if (opts.length < 2) return "Add at least two options.";
  if (v.type === "SingleCorrect" && correct !== 1) return "Mark exactly one option as correct.";
  if (v.type === "MultipleCorrect" && correct < 1) return "Mark at least one option as correct.";
  return null;
}

export function QuestionEditorPage() {
  const { id } = useParams<{ id?: string }>();
  const isNew = !id || id === "new";
  const q = useQuery({
    queryKey: ["admin", "question", id],
    queryFn: () => unwrap(api.GET("/api/v1/admin/questions/{id}", { params: { path: { id: id! } } })),
    enabled: !isNew,
  });
  if (!isNew && q.isPending) return <PageLoading />;
  if (!isNew && q.isError) return <ErrorState error={q.error} onRetry={() => q.refetch()} />;
  return <QuestionEditor key={q.data ? `${q.data.id}-${q.data.version}` : "new"} question={q.data} />;
}

function QuestionEditor({ question }: { question?: AdminQuestion }) {
  const router = useRouter();
  const params = useSearchParams();
  const { data: user } = useSession();
  const [topic, setTopic] = useState<TopicSelection>({
    courseId: question?.courseId ?? params.get("courseId") ?? "",
    chapterId: question?.chapterId ?? params.get("chapterId") ?? "",
    topicId: question?.topicId ?? params.get("topicId") ?? "",
  });
  const [formError, setFormError] = useState<string | null>(null);
  const [previewLang, setPreviewLang] = useState<"en" | "hi">("en");
  const [addAnother, setAddAnother] = useState(false);

  const { register, control, handleSubmit, watch, setValue, reset, formState } = useForm<Values>({ defaultValues: toValues(question) });
  const options = useFieldArray({ control, name: "options" });
  const v = watch();
  const errors = formState.errors;

  const save = useAction(
    (body: QuestionUpsert) =>
      question ? unwrap(api.PUT("/api/v1/admin/questions/{id}", { params: { path: { id: question.id } }, body })) : unwrap(api.POST("/api/v1/admin/questions", { body })),
    {
      invalidate: [["admin", "questions"], ["admin", "question"], ["admin", "course-tree"]],
      success: question ? `Saved as version ${question.version + 1}` : "Question created as draft",
      onSuccess: (r) => {
        if (question) return;
        if (addAnother) {
          reset({ ...toValues(), type: v.type, difficulty: v.difficulty, defaultMarks: v.defaultMarks, defaultNegativeMarks: v.defaultNegativeMarks });
          window.scrollTo({ top: 0 });
        } else router.push(`/admin/questions/${r.id}`);
      },
    },
  );
  const setStatus = useAction(
    (status: PublishStatus) => unwrap(api.POST("/api/v1/admin/questions/{id}/status", { params: { path: { id: question!.id } }, body: { status } })),
    { invalidate: [["admin", "questions"], ["admin", "question"]], success: "Status updated" },
  );

  const changeType = (t: QuestionType) => {
    setValue("type", t);
    if (t === "TrueFalse") setValue("options", TRUE_FALSE);
    else if (isOptionType(t) && (v.type === "TrueFalse" || v.options.length === 0)) setValue("options", EMPTY_OPTIONS);
    if (t !== "MultipleCorrect") setValue("allowPartialMarking", false);
  };

  const toggleCorrect = (index: number, checked: boolean) => {
    if (v.type === "MultipleCorrect") setValue(`options.${index}.isCorrect`, checked);
    else v.options.forEach((_, i) => setValue(`options.${i}.isCorrect`, i === index ? checked : false));
  };

  const submit = handleSubmit((values) => {
    setFormError(null);
    if (!topic.topicId) return setFormError("Choose the topic this question belongs to.");
    const problem = shapeError(values);
    if (problem) return setFormError(problem);
    const optionType = isOptionType(values.type);
    save.mutate({
      externalRef: blankToNull(values.externalRef),
      topicId: topic.topicId,
      type: values.type,
      difficulty: values.difficulty,
      stemEn: values.stemEn.trim(),
      stemHi: blankToNull(values.stemHi),
      imageUrl: blankToNull(values.imageUrl),
      explanationEn: blankToNull(values.explanationEn),
      explanationHi: blankToNull(values.explanationHi),
      hintEn: blankToNull(values.hintEn),
      videoSolutionId: blankToNull(values.videoSolutionId),
      numericAnswer: optionType ? null : Number(values.numericAnswer),
      numericTolerance: values.type === "Numerical" ? Number(values.numericTolerance) || 0 : 0,
      defaultMarks: Number(values.defaultMarks),
      defaultNegativeMarks: Number(values.defaultNegativeMarks),
      allowPartialMarking: values.type === "MultipleCorrect" && values.allowPartialMarking,
      previousYear: values.previousYear ? Number(values.previousYear) : null,
      options: optionType ? values.options.map((o) => ({ textEn: o.textEn.trim(), textHi: blankToNull(o.textHi), isCorrect: o.isCorrect })) : [],
    });
  });

  const markRules = { required: "Required", validate: (x: string) => (Number(x) >= 0 && Number(x) <= 100) || "Between 0 and 100" };

  return (
    <>
      <PageHeader
        back={<BackLink href="/admin/questions">Question bank</BackLink>}
        title={
          question ? (
            <span className="flex flex-wrap items-center gap-2">
              Edit question <StatusBadge status={question.status} />
            </span>
          ) : (
            "New question"
          )
        }
        description={
          question
            ? `Version ${question.version} · used in ${question.usedInTests} test${question.usedInTests === 1 ? "" : "s"}. Saving creates version ${question.version + 1}; past attempts keep the version students saw.`
            : "Saved as a draft. Publish it to make it available for practice and tests."
        }
        actions={question && can.publishContent(user?.roles) && <StatusActions status={question.status} noun="question" pending={setStatus.isPending} onChange={(s) => setStatus.mutate(s)} />}
      />

      <form onSubmit={submit} noValidate className="grid gap-6 xl:grid-cols-[1fr_22rem]">
        <div className="space-y-6">
          <Card title="Topic" className="p-0">
            <div className="p-4">
              <TopicPicker value={topic} onChange={setTopic} requireTopic idPrefix="qe" />
            </div>
          </Card>

          <Card title="Question" className="p-0">
            <div className="space-y-4 p-4">
              <div className="grid gap-4 sm:grid-cols-2">
                <Field label="Type" htmlFor="qe-type">
                  <Select id="qe-type" value={v.type} onChange={(e) => changeType(e.target.value as QuestionType)}>
                    {QUESTION_TYPES.map((t) => (
                      <option key={t} value={t}>
                        {humanize(t)}
                      </option>
                    ))}
                  </Select>
                </Field>
                <Field label="Difficulty" htmlFor="qe-diff">
                  <Select id="qe-diff" {...register("difficulty")}>
                    {DIFFICULTIES.map((d) => (
                      <option key={d}>{d}</option>
                    ))}
                  </Select>
                </Field>
              </div>
              <BilingualRow>
                <Field label="Question text (English)" htmlFor="qe-stem" required error={errors.stemEn?.message}>
                  <Textarea id="qe-stem" rows={5} {...register("stemEn", { required: "Question text is required" })} aria-invalid={!!errors.stemEn} />
                </Field>
                <Field label="Question text (Hindi)" htmlFor="qe-stem-hi">
                  <Textarea id="qe-stem-hi" rows={5} lang="hi" {...register("stemHi")} />
                </Field>
              </BilingualRow>
              <Field label="Image URL" htmlFor="qe-img" hint="Optional diagram, hosted on your CDN.">
                <Input id="qe-img" type="url" {...register("imageUrl")} />
              </Field>
            </div>
          </Card>

          {isOptionType(v.type) ? (
            <Card
              title={v.type === "MultipleCorrect" ? "Options (tick every correct one)" : "Options (tick the correct one)"}
              actions={
                v.type !== "TrueFalse" &&
                options.fields.length < 6 && (
                  <Button size="sm" variant="secondary" onClick={() => options.append({ textEn: "", textHi: "", isCorrect: false })}>
                    Add option
                  </Button>
                )
              }
            >
              <ol className="divide-y divide-border">
                {options.fields.map((f, i) => (
                  <li key={f.id} className="grid gap-3 p-4 sm:grid-cols-[auto_1fr_1fr_auto] sm:items-start">
                    <label className="flex items-center gap-2 pt-2 text-sm font-medium">
                      <input
                        type={v.type === "MultipleCorrect" ? "checkbox" : "radio"}
                        name="correct"
                        checked={v.options[i]?.isCorrect ?? false}
                        onChange={(e) => toggleCorrect(i, e.target.checked)}
                        aria-label={`Option ${String.fromCharCode(65 + i)} is correct`}
                        className="h-4 w-4 accent-[var(--success)]"
                      />
                      {String.fromCharCode(65 + i)}
                    </label>
                    <Input aria-label={`Option ${String.fromCharCode(65 + i)} (English)`} placeholder="English" readOnly={v.type === "TrueFalse"} {...register(`options.${i}.textEn`)} />
                    <Input aria-label={`Option ${String.fromCharCode(65 + i)} (Hindi)`} placeholder="Hindi (optional)" lang="hi" readOnly={v.type === "TrueFalse"} {...register(`options.${i}.textHi`)} />
                    {v.type !== "TrueFalse" && options.fields.length > 2 ? (
                      <Button size="sm" variant="ghost" onClick={() => options.remove(i)} aria-label={`Remove option ${String.fromCharCode(65 + i)}`}>
                        Remove
                      </Button>
                    ) : (
                      <span />
                    )}
                  </li>
                ))}
              </ol>
            </Card>
          ) : (
            <Card title="Answer" className="p-0">
              <div className="grid gap-4 p-4 sm:grid-cols-2">
                <Field label={v.type === "Integer" ? "Correct answer (whole number)" : "Correct answer"} htmlFor="qe-num" required>
                  <Input id="qe-num" type="number" step={v.type === "Integer" ? 1 : "any"} inputMode="decimal" {...register("numericAnswer")} />
                </Field>
                {v.type === "Numerical" && (
                  <Field label="Accepted tolerance (±)" htmlFor="qe-tol" hint="e.g. 0.01 accepts 9.80–9.82 for 9.81.">
                    <Input id="qe-tol" type="number" step="any" min={0} {...register("numericTolerance")} />
                  </Field>
                )}
              </div>
            </Card>
          )}

          <Card title="Marking" className="p-0">
            <div className="space-y-4 p-4">
              <div className="grid gap-4 sm:grid-cols-2">
                <Field label="Marks for correct answer" htmlFor="qe-marks" error={errors.defaultMarks?.message} hint="Default when added to a test; tests can override.">
                  <Input id="qe-marks" type="number" step="0.25" min={0} {...register("defaultMarks", markRules)} />
                </Field>
                <Field label="Marks deducted when wrong" htmlFor="qe-neg" error={errors.defaultNegativeMarks?.message} hint="Enter 1 for −1. Use 0 for no negative marking.">
                  <Input id="qe-neg" type="number" step="0.25" min={0} {...register("defaultNegativeMarks", markRules)} />
                </Field>
              </div>
              {v.type === "MultipleCorrect" && (
                <Checkbox label="Partial marking" hint="+1 per correct option when no wrong option is chosen (JEE Advanced style)." {...register("allowPartialMarking")} />
              )}
            </div>
          </Card>

          <Card title="Solution" className="p-0">
            <div className="space-y-4 p-4">
              <BilingualRow>
                <Field label="Explanation (English)" htmlFor="qe-exp" hint="Shown after the student answers.">
                  <Textarea id="qe-exp" rows={4} {...register("explanationEn")} />
                </Field>
                <Field label="Explanation (Hindi)" htmlFor="qe-exp-hi">
                  <Textarea id="qe-exp-hi" rows={4} lang="hi" {...register("explanationHi")} />
                </Field>
              </BilingualRow>
              <div className="grid gap-4 sm:grid-cols-2">
                <Field label="Hint" htmlFor="qe-hint" hint="Optional, shown in practice only.">
                  <Input id="qe-hint" {...register("hintEn")} />
                </Field>
                <Field label="Video solution ID" htmlFor="qe-vsol" hint="Optional provider video ID.">
                  <Input id="qe-vsol" {...register("videoSolutionId")} />
                </Field>
                <Field label="Previous-year paper" htmlFor="qe-year" hint="Year, if this question appeared in an exam (licensed content only)." error={errors.previousYear?.message}>
                  <Input id="qe-year" type="number" {...register("previousYear", { validate: (y) => !y || (Number(y) >= 1980 && Number(y) <= 2100) || "Enter a valid year" })} />
                </Field>
                <Field label="Reference ID" htmlFor="qe-ref" hint="Your own unique ID, used to match CSV imports.">
                  <Input id="qe-ref" {...register("externalRef")} />
                </Field>
              </div>
            </div>
          </Card>

          <InlineError error={formError ?? save.error} />
          <div className="flex flex-wrap justify-end gap-2">
            {!question && (
              <Button type="submit" variant="secondary" loading={save.isPending && addAnother} onClick={() => setAddAnother(true)}>
                Save and add another
              </Button>
            )}
            <Button type="submit" loading={save.isPending && !addAnother} onClick={() => setAddAnother(false)}>
              {question ? "Save changes" : "Save question"}
            </Button>
          </div>
        </div>

        <aside className="xl:sticky xl:top-6 xl:self-start">
          <Card
            title="Student preview"
            actions={
              <Select aria-label="Preview language" value={previewLang} onChange={(e) => setPreviewLang(e.target.value as "en" | "hi")} className="h-8 w-28">
                <option value="en">English</option>
                <option value="hi">Hindi</option>
              </Select>
            }
          >
            <QuestionPreview values={v} lang={previewLang} />
          </Card>
        </aside>
      </form>
    </>
  );
}

function QuestionPreview({ values, lang }: { values: Values; lang: "en" | "hi" }) {
  const pick = (en: string, hi: string) => (lang === "hi" && hi.trim() ? hi : en);
  const stem = pick(values.stemEn, values.stemHi);
  return (
    <div className="space-y-3 p-4 text-sm" lang={lang}>
      <p className="text-xs text-fg-muted">
        +{values.defaultMarks || 0} / −{values.defaultNegativeMarks || 0} · {humanize(values.type)}
      </p>
      <p className="whitespace-pre-wrap">{stem || <span className="text-fg-muted">Question text appears here.</span>}</p>
      {isOptionType(values.type) ? (
        <ul className="space-y-2">
          {values.options.map((o, i) => (
            <li key={i} className={`rounded-lg border px-3 py-2 ${o.isCorrect ? "border-success bg-success-soft" : "border-border"}`}>
              <span className="mr-2 font-medium">{String.fromCharCode(65 + i)}.</span>
              {pick(o.textEn, o.textHi) || <span className="text-fg-muted">Option</span>}
            </li>
          ))}
        </ul>
      ) : (
        <div className="rounded-lg border border-dashed border-border px-3 py-2 text-fg-muted">Answer box · correct: {values.numericAnswer || "—"}</div>
      )}
      {(values.explanationEn || values.explanationHi) && (
        <div className="rounded-lg bg-surface-muted px-3 py-2">
          <p className="text-xs font-semibold uppercase text-fg-muted">Explanation</p>
          <p className="whitespace-pre-wrap">{pick(values.explanationEn, values.explanationHi)}</p>
        </div>
      )}
      <p className="text-xs text-fg-muted">Correct answers are highlighted here only; students see them after answering.</p>
    </div>
  );
}
