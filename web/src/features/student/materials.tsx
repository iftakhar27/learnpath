"use client";

import Link from "next/link";
import { useState } from "react";
import { useQuery } from "@tanstack/react-query";
import { api, unwrap } from "@/lib/api/client";
import type { components } from "@/lib/api/schema";
import { useT, type MessageKey } from "@/lib/i18n";
import { Button, buttonClass } from "@/components/ui/button";
import { SkeletonRows } from "@/components/ui/feedback";
import { Icon, type IconName } from "@/components/ui/icons";
import { StudentError } from "./errors";
import { useIsGuest } from "./guest";
import { tone } from "./learn/subjects";
import { PageTitle } from "./ui";

type Material = components["schemas"]["MaterialDto"];
type MaterialType = components["schemas"]["MaterialType"];

export const TYPE_LABEL: Record<MaterialType, MessageKey> = {
  Notes: "materialTypeNotes",
  FormulaSheet: "materialTypeFormulaSheet",
  MindMap: "materialTypeMindMap",
  PreviousYearPaper: "materialTypePreviousYearPaper",
  SamplePaper: "materialTypeSamplePaper",
  Other: "materialTypeOther",
};
const SUBJECTS: { value: string; label: MessageKey }[] = [
  { value: "", label: "allSubjects" },
  { value: "Physics", label: "physicsShort" },
  { value: "Chemistry", label: "chemistryShort" },
  { value: "Biology", label: "biologyShort" },
  { value: "general", label: "subjectGeneral" },
];

const sizeLabel = (bytes: number) => (bytes >= 1024 * 1024 ? `${(bytes / 1024 / 1024).toFixed(1)} MB` : `${Math.max(1, Math.round(bytes / 1024))} KB`);

function Segmented<T extends string>({ label, value, options, onChange }: { label: string; value: T; options: { value: T; label: string }[]; onChange: (v: T) => void }) {
  return (
    // One row that scrolls sideways on phones; wraps on wider screens.
    <div role="group" aria-label={label} className="-mx-4 flex gap-1.5 overflow-x-auto px-4 pb-1 sm:mx-0 sm:flex-wrap sm:overflow-visible sm:px-0">
      {options.map((o) => (
        <button
          key={o.value}
          type="button"
          aria-pressed={value === o.value}
          onClick={() => onChange(o.value)}
          className={`shrink-0 whitespace-nowrap rounded-full border px-3 py-1.5 text-sm font-medium ${value === o.value ? "border-primary bg-primary text-primary-fg" : "border-border bg-surface hover:border-primary"}`}
        >
          {o.label}
        </button>
      ))}
    </div>
  );
}

/** Free downloads for everyone signed in; guests see the list and are asked to sign up to download. */
export function MaterialsPage() {
  const t = useT();
  const [subject, setSubject] = useState("");
  const [classLevel, setClassLevel] = useState("");
  const [type, setType] = useState<MaterialType | "">("");
  const q = useQuery({
    queryKey: ["materials", { subject, classLevel, type }],
    queryFn: () => unwrap(api.GET("/api/v1/materials", { params: { query: { subject: subject || undefined, classLevel: classLevel || undefined, type: type || undefined } } })),
    placeholderData: (prev) => prev,
  });

  return (
    <div className="space-y-5">
      <PageTitle subtitle={t("freeMaterialIntro")}>{t("freeMaterialTitle")}</PageTitle>
      <div className="space-y-3">
        <Segmented label={t("allSubjects")} value={subject} onChange={setSubject} options={SUBJECTS.map((s) => ({ value: s.value, label: t(s.label) }))} />
        <Segmented
          label={t("classLevel", { level: "" }).trim()}
          value={classLevel}
          onChange={setClassLevel}
          options={[
            { value: "", label: t("bothClasses") },
            { value: "11", label: t("classLevel", { level: "11" }) },
            { value: "12", label: t("classLevel", { level: "12" }) },
          ]}
        />
        <Segmented
          label={t("allTypes")}
          value={type}
          onChange={setType}
          options={[{ value: "" as const, label: t("allTypes") }, ...(Object.keys(TYPE_LABEL) as MaterialType[]).map((k) => ({ value: k, label: t(TYPE_LABEL[k]) }))]}
        />
      </div>
      {q.isPending ? (
        <SkeletonRows rows={4} />
      ) : q.isError ? (
        <StudentError error={q.error} onRetry={() => q.refetch()} />
      ) : q.data.length === 0 ? (
        <p className="rounded-2xl border border-dashed border-border p-6 text-center text-fg-muted">{t("materialsEmpty")}</p>
      ) : (
        <ul className="grid gap-3 md:grid-cols-2">
          {q.data.map((m) => (
            <MaterialCard key={m.id} m={m} />
          ))}
        </ul>
      )}
    </div>
  );
}

function MaterialCard({ m }: { m: Material }) {
  const t = useT();
  const guest = useIsGuest();
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<unknown>(null);
  const tn = m.subject ? tone(m.subject) : { soft: "bg-surface-muted", text: "text-fg-muted" };
  const icon: IconName = m.format === "Link" ? "external" : m.format === "Image" ? "target" : "doc";
  const subjectLabel = m.subject === "Physics" ? t("physicsShort") : m.subject === "Chemistry" ? t("chemistryShort") : m.subject === "Biology" ? t("biologyShort") : t("subjectGeneral");

  // The tab opens on the click itself (so pop-up blockers allow it), then goes to the signed link.
  const open = async () => {
    setError(null);
    setBusy(true);
    const tab = window.open("", "_blank");
    try {
      const link = await unwrap(api.POST("/api/v1/materials/{id}/download", { params: { path: { id: m.id } } }));
      if (tab) tab.location.href = link.url;
      else window.location.assign(link.url);
    } catch (e) {
      tab?.close();
      setError(e);
    } finally {
      setBusy(false);
    }
  };

  return (
    <li className="flex flex-col gap-3 rounded-2xl border border-border bg-surface p-4">
      <div className="flex items-start gap-3">
        <span className={`flex size-11 shrink-0 items-center justify-center rounded-xl ${tn.soft} ${tn.text}`}>
          <Icon name={icon} />
        </span>
        <div className="min-w-0 flex-1">
          <h2 className="font-semibold">{m.title}</h2>
          {m.description && <p className="mt-0.5 text-sm text-fg-muted">{m.description}</p>}
          <p className="mt-1 flex flex-wrap gap-x-2 text-xs text-fg-muted">
            <span>{subjectLabel}</span>
            <span aria-hidden>·</span>
            <span>{m.classLevel ? t("classLevel", { level: m.classLevel }) : t("bothClasses")}</span>
            <span aria-hidden>·</span>
            <span>{t(TYPE_LABEL[m.type as MaterialType] ?? "materialTypeOther")}</span>
          </p>
        </div>
      </div>
      <div className="mt-auto flex items-center justify-between gap-3">
        <span className="text-sm text-fg-muted">{m.format === "Link" ? t("materialLink") : `${m.format === "PDF" ? "PDF" : t("materialImage")} · ${sizeLabel(m.sizeBytes)}`}</span>
        {guest ? (
          <Link href={`/register?next=${encodeURIComponent("/materials")}`} className={buttonClass("secondary", "sm")}>
            {t("signUpToDownload")}
          </Link>
        ) : (
          <Button size="sm" onClick={() => void open()} loading={busy} aria-label={`${m.format === "Link" ? t("openLink") : t("download")}: ${m.title}`}>
            <Icon name={m.format === "Link" ? "external" : "doc"} size={16} />
            {m.format === "Link" ? t("openLink") : t("download")}
          </Button>
        )}
      </div>
      <StudentError error={error} />
    </li>
  );
}
