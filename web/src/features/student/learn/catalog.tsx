"use client";

import Link from "next/link";
import { useDeferredValue, useState } from "react";
import { useQuery } from "@tanstack/react-query";
import { api, unwrap } from "@/lib/api/client";
import { useT } from "@/lib/i18n";
import { SkeletonRows } from "@/components/ui/feedback";
import { Input, Select } from "@/components/ui/field";
import { Icon } from "@/components/ui/icons";
import { StudentError } from "../errors";
import { useProfile } from "../profile";
import { routes } from "../routes";
import { Muted, PageTitle, useFormat } from "../ui";
import { SubjectGrid } from "./subjects";

type PriceFilter = "all" | "free" | "paid";

export function Catalog() {
  const t = useT();
  const f = useFormat();
  const profile = useProfile();
  const [q, setQ] = useState("");
  const query = useDeferredValue(q.trim());
  const [price, setPrice] = useState<PriceFilter>("all");
  const [examChoice, setExamChoice] = useState<string | null>(null);
  const examId = examChoice ?? profile.data?.targetExamId ?? "";
  const exams = useQuery({ queryKey: ["exams"], queryFn: () => unwrap(api.GET("/api/v1/exams")) });
  const courses = useQuery({
    queryKey: ["student", "courses", { examId, query, price }],
    queryFn: () =>
      unwrap(
        api.GET("/api/v1/courses", {
          params: { query: { examId: examId || undefined, q: query || undefined, free: price === "all" ? undefined : price === "free", pageSize: 50 } },
        }),
      ),
    placeholderData: (prev) => prev,
  });

  return (
    <>
      <PageTitle>{t("tabLearn")}</PageTitle>
      <div className="mb-8">
        <SubjectGrid />
      </div>
      <Link href={routes.materials} className="mb-8 flex items-center gap-3 rounded-2xl border border-border bg-surface p-4 hover:border-primary">
        <span className="rounded-xl bg-primary-soft p-2 text-primary">
          <Icon name="doc" />
        </span>
        <span className="min-w-0 flex-1">
          <span className="block font-semibold">{t("freeMaterialTitle")}</span>
          <span className="block text-sm text-fg-muted">{t("freeMaterialIntro")}</span>
        </span>
        <Icon name="chevronRight" className="text-fg-muted" />
      </Link>
      <h2 className="mb-3 text-lg font-semibold">{t("coursesHeading")}</h2>
      <div role="search" aria-label={t("filterCourses")} className="mb-6 flex flex-wrap items-center gap-3">
        <Input type="search" value={q} onChange={(e) => setQ(e.target.value)} placeholder={t("searchHint")} aria-label={t("search")} className="w-full sm:w-72" />
        <Select value={examId} onChange={(e) => setExamChoice(e.target.value)} aria-label={t("chooseExam")} className="w-full sm:w-56">
          <option value="">{t("allExams")}</option>
          {exams.data?.map((e) => (
            <option key={e.id} value={e.id}>
              {e.name}
            </option>
          ))}
        </Select>
        <div role="radiogroup" aria-label={t("filterCourses")} className="flex gap-1 rounded-lg bg-surface-muted p-1 text-sm">
          {(["all", "free", "paid"] as const).map((p) => (
            <button
              key={p}
              type="button"
              role="radio"
              aria-checked={price === p}
              onClick={() => setPrice(p)}
              className={`rounded-md px-3 py-1.5 font-medium ${price === p ? "bg-surface text-fg shadow-sm" : "text-fg-muted hover:text-fg"}`}
            >
              {p === "all" ? t("allCourses") : p === "free" ? t("free") : t("paid")}
            </button>
          ))}
        </div>
      </div>

      {courses.isPending ? (
        <SkeletonRows rows={4} />
      ) : courses.isError ? (
        <StudentError error={courses.error} onRetry={() => courses.refetch()} />
      ) : courses.data.items.length === 0 ? (
        <Muted>{t("coursesEmpty")}</Muted>
      ) : (
        <ul className="grid gap-4 sm:grid-cols-2 lg:grid-cols-3">
          {courses.data.items.map((c) => (
            <li key={c.id}>
              <Link href={routes.course(c.id)} className="flex h-full flex-col rounded-2xl border border-border bg-surface p-5 hover:border-primary">
                <span className="flex flex-wrap gap-2 text-xs font-medium">
                  {c.enrolled ? (
                    <span className="rounded-full bg-success-soft px-2 py-0.5 text-success">{t("enrolled")}</span>
                  ) : c.isFree ? (
                    <span className="rounded-full bg-primary-soft px-2 py-0.5 text-primary">{t("free")}</span>
                  ) : null}
                  {c.classLevel && <span className="rounded-full bg-surface-muted px-2 py-0.5 text-fg-muted">{t("classLabel", { level: c.classLevel })}</span>}
                  {!c.isFree && c.lessonCount === 0 && c.testCount === 0 && !c.enrolled && <span className="rounded-full bg-warning-soft px-2 py-0.5 text-warning">{t("classesStartSoon")}</span>}
                </span>
                <span className="mt-3 text-lg font-semibold">{c.title}</span>
                {c.description && <span className="mt-1 line-clamp-2 text-sm text-fg-muted">{c.description}</span>}
                <span className="mt-auto pt-4 text-sm font-medium">
                  {c.isFree ? t("free") : f.rupees(c.priceInPaise)}
                  {!c.isFree && <span className="font-normal text-fg-muted"> · {c.accessDays ? t("accessDays", { days: c.accessDays }) : t("lifetimeAccess")}</span>}
                </span>
              </Link>
            </li>
          ))}
        </ul>
      )}
    </>
  );
}
