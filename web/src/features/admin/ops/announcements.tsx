"use client";

import { useState } from "react";
import { useForm } from "react-hook-form";
import { api, unwrap } from "@/lib/api/client";
import { Button } from "@/components/ui/button";
import { ConfirmDialog } from "@/components/ui/dialog";
import { Field, Input, Textarea } from "@/components/ui/field";
import { Card, PageHeader } from "@/components/ui/layout";
import { CourseSelect, useAction, useCourses } from "../shared";

type Values = { title: string; body: string };

export function Announcements() {
  const courses = useCourses();
  const [courseId, setCourseId] = useState("");
  const [pending, setPending] = useState<Values | null>(null);
  const { register, handleSubmit, reset, formState } = useForm<Values>({ defaultValues: { title: "", body: "" } });
  const send = useAction((v: Values) => unwrap(api.POST("/api/v1/admin/announcements", { body: { title: v.title.trim(), body: v.body.trim(), courseId: courseId || null } })), {
    success: (r) => `Sent to ${r.recipients} student${r.recipients === 1 ? "" : "s"}`,
    onSuccess: () => {
      setPending(null);
      reset();
    },
  });
  const audience = courseId ? `students enrolled in "${courses.data?.find((c) => c.id === courseId)?.titleEn ?? "this course"}"` : "every active student";

  return (
    <>
      <PageHeader title="Announcements" description="Send an in-app notification. Push and email delivery arrive in Phase 2." />
      <Card className="max-w-2xl p-4 sm:p-6">
        <form noValidate className="space-y-4" onSubmit={handleSubmit((v) => setPending(v))}>
          <Field label="Audience" htmlFor="ann-course" hint="Leave as 'All students' to reach everyone.">
            <CourseSelect id="ann-course" value={courseId} onChange={setCourseId} allowEmpty="All students" />
          </Field>
          <Field label="Title" htmlFor="ann-title" required error={formState.errors.title?.message}>
            <Input id="ann-title" maxLength={200} {...register("title", { required: "Title is required" })} />
          </Field>
          <Field label="Message" htmlFor="ann-body" required error={formState.errors.body?.message} hint="Plain text, up to 2000 characters. Write in English or Hindi.">
            <Textarea id="ann-body" rows={6} maxLength={2000} {...register("body", { required: "Message is required" })} />
          </Field>
          <div className="flex justify-end">
            <Button type="submit">Review and send</Button>
          </div>
        </form>
      </Card>
      <ConfirmDialog
        open={pending !== null}
        title="Send announcement?"
        message={
          <>
            <strong>{pending?.title}</strong> will go to {audience}. This can&apos;t be unsent.
          </>
        }
        confirmLabel="Send"
        loading={send.isPending}
        onCancel={() => setPending(null)}
        onConfirm={() => pending && send.mutate(pending)}
      />
    </>
  );
}
