"use client";

import { useForm } from "react-hook-form";
import type { AdminCourse, CourseUpsert } from "@/lib/api/types";
import { blankToNull, fromLocalInput, paiseToRupees, rupeesToPaise, toLocalInput } from "@/lib/format";
import { Button } from "@/components/ui/button";
import { BilingualRow, Checkbox, Field, Input, Textarea } from "@/components/ui/field";
import { ExamSelect } from "../shared";

type Values = {
  examId: string;
  slug: string;
  titleEn: string;
  titleHi: string;
  descriptionEn: string;
  descriptionHi: string;
  classLevel: string;
  board: string;
  isFree: boolean;
  priceRupees: string;
  accessDays: string;
  publishAt: string;
};

export const slugify = (s: string) =>
  s
    .toLowerCase()
    .normalize("NFKD")
    .replace(/[^a-z0-9]+/g, "-")
    .replace(/^-+|-+$/g, "")
    .slice(0, 120);

export function CourseForm({
  course,
  onSubmit,
  submitLabel,
  pending,
}: {
  course?: AdminCourse;
  onSubmit: (body: CourseUpsert) => void;
  submitLabel: string;
  pending?: boolean;
}) {
  const { register, handleSubmit, watch, setValue, formState } = useForm<Values>({
    defaultValues: {
      examId: course?.examId ?? "",
      slug: course?.slug ?? "",
      titleEn: course?.titleEn ?? "",
      titleHi: course?.titleHi ?? "",
      descriptionEn: course?.descriptionEn ?? "",
      descriptionHi: course?.descriptionHi ?? "",
      classLevel: course?.classLevel ?? "",
      board: course?.board ?? "",
      isFree: course?.isFree ?? false,
      priceRupees: course && !course.isFree ? String(paiseToRupees(course.priceInPaise)) : "",
      accessDays: course?.accessDays != null ? String(course.accessDays) : "365",
      publishAt: toLocalInput(course?.publishAt),
    },
  });
  const isFree = watch("isFree");
  const examId = watch("examId");
  const errors = formState.errors;

  const submit = handleSubmit((v) =>
    onSubmit({
      examId: v.examId,
      slug: v.slug.trim(),
      titleEn: v.titleEn.trim(),
      titleHi: blankToNull(v.titleHi),
      descriptionEn: blankToNull(v.descriptionEn),
      descriptionHi: blankToNull(v.descriptionHi),
      classLevel: blankToNull(v.classLevel),
      board: blankToNull(v.board),
      isFree: v.isFree,
      priceInPaise: v.isFree ? 0 : rupeesToPaise(Number(v.priceRupees)),
      accessDays: v.accessDays ? Number(v.accessDays) : null,
      publishAt: fromLocalInput(v.publishAt),
    }),
  );

  const titleField = register("titleEn", {
    required: "Title is required",
    maxLength: { value: 200, message: "Keep it under 200 characters" },
  });

  return (
    <form onSubmit={submit} className="space-y-4" noValidate>
      <input type="hidden" {...register("examId", { required: "Select an exam" })} />
      <Field label="Exam" htmlFor="course-exam" required error={errors.examId?.message}>
        <ExamSelect id="course-exam" value={examId} onChange={(v) => setValue("examId", v, { shouldValidate: true })} />
      </Field>
      <BilingualRow>
        <Field label="Title (English)" htmlFor="course-title" required error={errors.titleEn?.message}>
          <Input
            id="course-title"
            {...titleField}
            onChange={(e) => {
              titleField.onChange(e);
              if (!course && !formState.dirtyFields.slug) setValue("slug", slugify(e.target.value));
            }}
            aria-invalid={!!errors.titleEn}
          />
        </Field>
        <Field label="Title (Hindi)" htmlFor="course-title-hi" hint="Optional. Students who choose Hindi see this.">
          <Input id="course-title-hi" lang="hi" {...register("titleHi")} />
        </Field>
      </BilingualRow>
      <Field label="URL slug" htmlFor="course-slug" required error={errors.slug?.message} hint="Lowercase letters, numbers and hyphens. Must be unique.">
        <Input
          id="course-slug"
          {...register("slug", { required: "Slug is required", pattern: { value: /^[a-z0-9-]+$/, message: "Use lowercase letters, numbers and hyphens only" } })}
          aria-invalid={!!errors.slug}
        />
      </Field>
      <BilingualRow>
        <Field label="Description (English)" htmlFor="course-desc">
          <Textarea id="course-desc" {...register("descriptionEn")} />
        </Field>
        <Field label="Description (Hindi)" htmlFor="course-desc-hi">
          <Textarea id="course-desc-hi" lang="hi" {...register("descriptionHi")} />
        </Field>
      </BilingualRow>
      <div className="grid gap-4 sm:grid-cols-2">
        <Field label="Class" htmlFor="course-class" hint="e.g. 11, 12, Dropper">
          <Input id="course-class" {...register("classLevel")} />
        </Field>
        <Field label="Board" htmlFor="course-board" hint="e.g. CBSE, UP Board">
          <Input id="course-board" {...register("board")} />
        </Field>
      </div>
      <Checkbox label="Free course" hint="Anyone can enrol without paying." {...register("isFree")} />
      {!isFree && (
        <div className="grid gap-4 sm:grid-cols-2">
          <Field label="Price (₹, GST-inclusive)" htmlFor="course-price" required error={errors.priceRupees?.message}>
            <Input
              id="course-price"
              type="number"
              min={1}
              step="0.01"
              inputMode="decimal"
              {...register("priceRupees", {
                validate: (v) => isFree || (Number(v) >= 1 ? true : "Paid courses must cost at least ₹1"),
              })}
              aria-invalid={!!errors.priceRupees}
            />
          </Field>
          <Field label="Access period (days)" htmlFor="course-days" hint="Leave empty for lifetime access." error={errors.accessDays?.message}>
            <Input id="course-days" type="number" min={1} max={3650} {...register("accessDays", { validate: (v) => !v || (Number(v) >= 1 && Number(v) <= 3650) || "Between 1 and 3650 days" })} />
          </Field>
        </div>
      )}
      <Field label="Visible from" htmlFor="course-publish-at" hint="Optional. A published course stays hidden until this time (your local time).">
        <Input id="course-publish-at" type="datetime-local" {...register("publishAt")} />
      </Field>
      <div className="flex justify-end">
        <Button type="submit" loading={pending}>
          {submitLabel}
        </Button>
      </div>
    </form>
  );
}
