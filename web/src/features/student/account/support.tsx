"use client";

import { useState } from "react";
import { useForm } from "react-hook-form";
import { useQuery, useQueryClient } from "@tanstack/react-query";
import { api, unwrap } from "@/lib/api/client";
import { useT, type MessageKey } from "@/lib/i18n";
import { Button } from "@/components/ui/button";
import { PageLoading } from "@/components/ui/feedback";
import { Field, Input, Select, Textarea } from "@/components/ui/field";
import { useToast } from "@/components/ui/toast";
import { StudentError } from "../errors";
import { Muted, PageTitle, Section, useFormat } from "../ui";

const CATEGORIES: [string, MessageKey][] = [
  ["payment", "categoryPayment"],
  ["content", "categoryContent"],
  ["technical", "categoryTechnical"],
  ["account", "categoryAccount"],
  ["other", "categoryOther"],
];

export function SupportPage() {
  const t = useT();
  const f = useFormat();
  const toast = useToast();
  const qc = useQueryClient();
  const tickets = useQuery({ queryKey: ["student", "tickets"], queryFn: () => unwrap(api.GET("/api/v1/support/tickets")) });
  const [error, setError] = useState<unknown>(null);
  const { register, handleSubmit, formState, reset } = useForm<{ category: string; subject: string; message: string }>({ defaultValues: { category: "payment" } });

  const submit = handleSubmit(async (v) => {
    setError(null);
    try {
      await unwrap(api.POST("/api/v1/support/tickets", { body: { category: v.category, subject: v.subject.trim(), message: v.message.trim() } }));
      reset({ category: v.category, subject: "", message: "" });
      toast(t("ticketSent"));
      await qc.invalidateQueries({ queryKey: ["student", "tickets"] });
    } catch (e) {
      setError(e);
    }
  });

  return (
    <div className="mx-auto max-w-2xl space-y-6">
      <PageTitle>{t("helpSupport")}</PageTitle>
      <Section title={t("newTicket")}>
        <form onSubmit={submit} className="space-y-4" noValidate>
          <StudentError error={error} />
          <Field label={t("ticketCategory")}>
            <Select {...register("category")}>
              {CATEGORIES.map(([v, k]) => (
                <option key={v} value={v}>
                  {t(k)}
                </option>
              ))}
            </Select>
          </Field>
          <Field label={t("ticketSubject")} error={formState.errors.subject?.message}>
            <Input maxLength={200} aria-invalid={!!formState.errors.subject} {...register("subject", { validate: (v) => !!v.trim() || t("fieldRequired") })} />
          </Field>
          <Field label={t("ticketMessage")} error={formState.errors.message?.message}>
            <Textarea rows={5} maxLength={4000} aria-invalid={!!formState.errors.message} {...register("message", { validate: (v) => !!v.trim() || t("fieldRequired") })} />
          </Field>
          <Button type="submit" loading={formState.isSubmitting}>
            {t("send")}
          </Button>
        </form>
      </Section>
      {tickets.isPending ? (
        <PageLoading />
      ) : tickets.isError ? (
        <StudentError error={tickets.error} onRetry={() => tickets.refetch()} />
      ) : tickets.data.length === 0 ? (
        <Muted>{t("ticketsEmpty")}</Muted>
      ) : (
        <ul className="space-y-3">
          {tickets.data.map((tk) => (
            <li key={tk.id} className="rounded-2xl border border-border bg-surface p-4">
              <div className="flex flex-wrap items-start justify-between gap-2">
                <h2 className="font-medium">{tk.subject}</h2>
                <span className="text-xs text-fg-muted">
                  {t("ticketStatus")}: {tk.status} · {f.date(tk.createdAt)}
                </span>
              </div>
              <p className="mt-1 whitespace-pre-line text-sm text-fg-muted">{tk.message}</p>
              {tk.staffResponse && (
                <div className="mt-3 rounded-xl bg-primary-soft p-3 text-sm">
                  <p className="font-medium">{t("supportReply")}</p>
                  <p className="mt-1 whitespace-pre-line">{tk.staffResponse}</p>
                </div>
              )}
            </li>
          ))}
        </ul>
      )}
    </div>
  );
}
