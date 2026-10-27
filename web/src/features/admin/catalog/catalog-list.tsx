"use client";

import Link from "next/link";
import { useRouter } from "next/navigation";
import { useState } from "react";
import { useForm } from "react-hook-form";
import { api, unwrap } from "@/lib/api/client";
import type { AdminExam, CourseUpsert, ExamUpsert } from "@/lib/api/types";
import { blankToNull, formatDate, formatDateTime, formatNumber, formatPaise } from "@/lib/format";
import { can, useSession } from "@/lib/session";
import { Button } from "@/components/ui/button";
import { Modal } from "@/components/ui/dialog";
import { Badge, EmptyState, ErrorState, SkeletonRows, StatusBadge } from "@/components/ui/feedback";
import { Checkbox, Field, Input, Select } from "@/components/ui/field";
import { Card, PageHeader, Table, Td, Th } from "@/components/ui/layout";
import { CourseForm } from "./course-form";
import { useAction, useCourses, useExams } from "../shared";

export function CatalogList() {
  const { data: user } = useSession();
  const router = useRouter();
  const courses = useCourses();
  const exams = useExams();
  const [examFilter, setExamFilter] = useState("");
  const [creating, setCreating] = useState(false);
  const [editingExam, setEditingExam] = useState<AdminExam | "new" | null>(null);

  const create = useAction((body: CourseUpsert) => unwrap(api.POST("/api/v1/admin/courses", { body })), {
    invalidate: [["admin", "courses"]],
    success: "Course created as a draft",
    onSuccess: (r) => router.push(`/admin/catalog/${r.id}`),
  });

  const examName = (id: string) => exams.data?.find((e) => e.id === id)?.nameEn ?? "—";
  const list = (courses.data ?? []).filter((c) => !examFilter || c.examId === examFilter);

  return (
    <>
      <PageHeader
        title="Courses & syllabus"
        description="Create courses and build their syllabus: subjects, chapters, topics, videos and notes."
        actions={<Button onClick={() => setCreating(true)}>New course</Button>}
      />

      <Card
        title="Courses"
        actions={
          <Select aria-label="Filter by exam" value={examFilter} onChange={(e) => setExamFilter(e.target.value)} className="h-8 w-48">
            <option value="">All exams</option>
            {exams.data?.map((e) => (
              <option key={e.id} value={e.id}>
                {e.nameEn}
              </option>
            ))}
          </Select>
        }
      >
        {courses.isPending ? (
          <SkeletonRows />
        ) : courses.isError ? (
          <ErrorState error={courses.error} onRetry={() => courses.refetch()} />
        ) : list.length === 0 ? (
          <EmptyState title="No courses yet" action={<Button onClick={() => setCreating(true)}>Create the first course</Button>}>
            A course belongs to one exam and holds the syllabus students study.
          </EmptyState>
        ) : (
          <Table>
            <thead>
              <tr>
                <Th>Course</Th>
                <Th>Exam</Th>
                <Th>Price</Th>
                <Th>Status</Th>
                <Th className="text-right">Enrolled</Th>
                <Th>Updated</Th>
              </tr>
            </thead>
            <tbody>
              {list.map((c) => (
                <tr key={c.id} className="hover:bg-surface-muted/50">
                  <Td>
                    <Link href={`/admin/catalog/${c.id}`} className="font-medium text-primary hover:underline">
                      {c.titleEn}
                    </Link>
                    <div className="text-xs text-fg-muted">{c.slug}</div>
                  </Td>
                  <Td>{examName(c.examId)}</Td>
                  <Td>{c.isFree ? <Badge tone="primary">Free</Badge> : <span className="tabular-nums">{formatPaise(c.priceInPaise)}</span>}</Td>
                  <Td>
                    <StatusBadge status={c.status} />
                  </Td>
                  <Td className="text-right tabular-nums">{formatNumber(c.enrollments)}</Td>
                  <Td className="whitespace-nowrap text-fg-muted">{formatDateTime(c.updatedAt)}</Td>
                </tr>
              ))}
            </tbody>
          </Table>
        )}
      </Card>

      <Card
        title="Exams"
        className="mt-6"
        actions={
          can.publishContent(user?.roles) && (
            <Button size="sm" variant="secondary" onClick={() => setEditingExam("new")}>
              Add exam
            </Button>
          )
        }
      >
        {exams.isPending ? (
          <SkeletonRows rows={3} />
        ) : exams.isError ? (
          <ErrorState error={exams.error} onRetry={() => exams.refetch()} />
        ) : (
          <Table>
            <thead>
              <tr>
                <Th>Code</Th>
                <Th>Name</Th>
                <Th>Next exam date</Th>
                <Th>Status</Th>
                <Th />
              </tr>
            </thead>
            <tbody>
              {exams.data.map((e) => (
                <tr key={e.id}>
                  <Td className="font-mono text-xs">{e.code}</Td>
                  <Td>
                    {e.nameEn}
                    {e.nameHi && <div className="text-xs text-fg-muted" lang="hi">{e.nameHi}</div>}
                  </Td>
                  <Td>{e.nextExamDate ? formatDate(e.nextExamDate) : "—"}</Td>
                  <Td>{e.isActive ? <Badge tone="success">Active</Badge> : <Badge>Hidden</Badge>}</Td>
                  <Td className="text-right">
                    {can.publishContent(user?.roles) && (
                      <Button size="sm" variant="ghost" onClick={() => setEditingExam(e)}>
                        Edit
                      </Button>
                    )}
                  </Td>
                </tr>
              ))}
            </tbody>
          </Table>
        )}
      </Card>

      <Modal open={creating} onClose={() => setCreating(false)} title="New course" wide>
        <CourseForm submitLabel="Create course" pending={create.isPending} onSubmit={(body) => create.mutate(body)} />
      </Modal>
      {editingExam && <ExamModal exam={editingExam === "new" ? undefined : editingExam} onClose={() => setEditingExam(null)} />}
    </>
  );
}

type ExamValues = { code: string; nameEn: string; nameHi: string; nextExamDate: string; isActive: boolean; sortOrder: string };

function ExamModal({ exam, onClose }: { exam?: AdminExam; onClose: () => void }) {
  const { register, handleSubmit, formState } = useForm<ExamValues>({
    defaultValues: {
      code: exam?.code ?? "",
      nameEn: exam?.nameEn ?? "",
      nameHi: exam?.nameHi ?? "",
      nextExamDate: exam?.nextExamDate ?? "",
      isActive: exam?.isActive ?? true,
      sortOrder: String(exam?.sortOrder ?? 0),
    },
  });
  const save = useAction(
    (body: ExamUpsert) => (exam ? unwrap(api.PUT("/api/v1/admin/exams/{id}", { params: { path: { id: exam.id } }, body })) : unwrap(api.POST("/api/v1/admin/exams", { body }))),
    { invalidate: [["admin", "exams"]], success: exam ? "Exam updated" : "Exam added", onSuccess: onClose },
  );
  const errors = formState.errors;
  return (
    <Modal open onClose={onClose} title={exam ? `Edit ${exam.code}` : "Add exam"}>
      <form
        className="space-y-4"
        noValidate
        onSubmit={handleSubmit((v) =>
          save.mutate({ code: v.code.trim().toUpperCase(), nameEn: v.nameEn.trim(), nameHi: blankToNull(v.nameHi), nextExamDate: v.nextExamDate || null, isActive: v.isActive, sortOrder: Number(v.sortOrder) || 0 }),
        )}
      >
        <Field label="Code" htmlFor="exam-code" required error={errors.code?.message} hint="Short identifier, e.g. NEET or JEE_MAIN">
          <Input id="exam-code" {...register("code", { required: "Code is required", pattern: { value: /^[A-Za-z0-9_]+$/, message: "Letters, numbers and underscores only" } })} />
        </Field>
        <Field label="Name (English)" htmlFor="exam-name" required error={errors.nameEn?.message}>
          <Input id="exam-name" {...register("nameEn", { required: "Name is required" })} />
        </Field>
        <Field label="Name (Hindi)" htmlFor="exam-name-hi">
          <Input id="exam-name-hi" lang="hi" {...register("nameHi")} />
        </Field>
        <div className="grid gap-4 sm:grid-cols-2">
          <Field label="Next exam date" htmlFor="exam-date" hint="Drives the student countdown.">
            <Input id="exam-date" type="date" {...register("nextExamDate")} />
          </Field>
          <Field label="Sort order" htmlFor="exam-sort">
            <Input id="exam-sort" type="number" {...register("sortOrder")} />
          </Field>
        </div>
        <Checkbox label="Active (shown to students)" {...register("isActive")} />
        <div className="flex justify-end gap-2">
          <Button variant="secondary" onClick={onClose}>
            Cancel
          </Button>
          <Button type="submit" loading={save.isPending}>
            Save
          </Button>
        </div>
      </form>
    </Modal>
  );
}
