"use client";

import { useState } from "react";
import { useQuery, useQueryClient } from "@tanstack/react-query";
import { api, rawFetch, unwrap } from "@/lib/api/client";
import type { components } from "@/lib/api/schema";
import { Button } from "@/components/ui/button";
import { ConfirmDialog, Modal } from "@/components/ui/dialog";
import { EmptyState, ErrorState, InlineError, SkeletonRows, StatusBadge } from "@/components/ui/feedback";
import { BilingualRow, Field, Input, Select, Textarea } from "@/components/ui/field";
import { Card, PageHeader, Table, Td, Th } from "@/components/ui/layout";
import { useToast } from "@/components/ui/toast";
import { useAction } from "./shared";

type Material = components["schemas"]["AdminMaterialDto"];
type MaterialType = components["schemas"]["MaterialType"];

const key = ["admin", "materials"] as const;
const TYPES: { value: MaterialType; label: string }[] = [
  { value: "Notes", label: "Notes" },
  { value: "FormulaSheet", label: "Formula sheet" },
  { value: "MindMap", label: "Mind map" },
  { value: "PreviousYearPaper", label: "Previous year paper" },
  { value: "SamplePaper", label: "Sample paper" },
  { value: "Other", label: "Other" },
];
const typeLabel = (t: MaterialType) => TYPES.find((x) => x.value === t)?.label ?? t;
const size = (bytes: number) => (bytes >= 1024 * 1024 ? `${(bytes / 1024 / 1024).toFixed(1)} MB` : `${Math.max(1, Math.round(bytes / 1024))} KB`);

type FormValues = {
  titleEn: string;
  titleHi: string;
  descriptionEn: string;
  descriptionHi: string;
  type: MaterialType;
  subject: string;
  classLevel: string;
  status: "Published" | "Draft";
  source: "file" | "link";
  externalUrl: string;
};

function initial(m?: Material): FormValues {
  return {
    titleEn: m?.titleEn ?? "",
    titleHi: m?.titleHi ?? "",
    descriptionEn: m?.descriptionEn ?? "",
    descriptionHi: m?.descriptionHi ?? "",
    type: m?.type ?? "Notes",
    subject: m?.subject ?? "",
    classLevel: m?.classLevel ?? "",
    status: m?.status === "Draft" ? "Draft" : "Published",
    source: m?.format === "Link" ? "link" : "file",
    externalUrl: m?.externalUrl ?? "",
  };
}

/** Add or edit one material: details, and a file (PDF or image) or a link. Editing without a new file keeps the current one. */
function MaterialForm({ material, maxMb, onDone }: { material?: Material; maxMb: number; onDone: () => void }) {
  const toast = useToast();
  const qc = useQueryClient();
  const [v, setV] = useState(() => initial(material));
  const [file, setFile] = useState<File | null>(null);
  const [saving, setSaving] = useState(false);
  const [error, setError] = useState<unknown>(null);
  const set = (k: keyof FormValues) => (e: { target: { value: string } }) => setV({ ...v, [k]: e.target.value });
  const tooBig = file !== null && file.size > maxMb * 1024 * 1024;

  const submit = async (e: React.FormEvent) => {
    e.preventDefault();
    setError(null);
    const form = new FormData();
    form.append("titleEn", v.titleEn);
    form.append("type", v.type);
    form.append("status", v.status);
    for (const k of ["titleHi", "descriptionEn", "descriptionHi", "subject", "classLevel"] as const) if (v[k].trim()) form.append(k, v[k].trim());
    if (v.source === "file" && file) form.append("file", file);
    if (v.source === "link" && v.externalUrl.trim() && v.externalUrl.trim() !== material?.externalUrl) form.append("externalUrl", v.externalUrl.trim());
    setSaving(true);
    try {
      await rawFetch(material ? `/api/v1/admin/materials/${material.id}` : "/api/v1/admin/materials", { method: material ? "PUT" : "POST", body: form });
      await qc.invalidateQueries({ queryKey: key });
      toast(material ? "Material saved" : "Material added");
      onDone();
    } catch (err) {
      setError(err);
    } finally {
      setSaving(false);
    }
  };

  const needsSource = !material || (v.source === "link" && material.format !== "Link") || (v.source === "file" && material.format === "Link");
  return (
    <form onSubmit={submit} className="space-y-4">
      <InlineError error={error} />
      <BilingualRow>
        <Field label="Title (English)" required>
          <Input required maxLength={200} value={v.titleEn} onChange={set("titleEn")} />
        </Field>
        <Field label="Title (Hindi)">
          <Input maxLength={200} value={v.titleHi} onChange={set("titleHi")} lang="hi" />
        </Field>
      </BilingualRow>
      <BilingualRow>
        <Field label="Description (English)">
          <Textarea rows={2} maxLength={1000} value={v.descriptionEn} onChange={set("descriptionEn")} />
        </Field>
        <Field label="Description (Hindi)">
          <Textarea rows={2} maxLength={1000} value={v.descriptionHi} onChange={set("descriptionHi")} lang="hi" />
        </Field>
      </BilingualRow>
      <div className="grid gap-4 sm:grid-cols-4">
        <Field label="Type">
          <Select value={v.type} onChange={set("type")}>
            {TYPES.map((t) => (
              <option key={t.value} value={t.value}>
                {t.label}
              </option>
            ))}
          </Select>
        </Field>
        <Field label="Subject">
          <Select value={v.subject} onChange={set("subject")}>
            <option value="">General</option>
            <option>Physics</option>
            <option>Chemistry</option>
            <option>Biology</option>
          </Select>
        </Field>
        <Field label="Class">
          <Select value={v.classLevel} onChange={set("classLevel")}>
            <option value="">11 and 12</option>
            <option value="11">Class 11</option>
            <option value="12">Class 12</option>
          </Select>
        </Field>
        <Field label="Status">
          <Select value={v.status} onChange={set("status")}>
            <option value="Published">Published</option>
            <option value="Draft">Draft (hidden)</option>
          </Select>
        </Field>
      </div>
      <fieldset className="space-y-3">
        <legend className="text-sm font-medium">Source</legend>
        <div className="flex gap-4 text-sm">
          <label className="flex items-center gap-2">
            <input type="radio" name="source" checked={v.source === "file"} onChange={() => setV({ ...v, source: "file" })} /> Upload a file
          </label>
          <label className="flex items-center gap-2">
            <input type="radio" name="source" checked={v.source === "link"} onChange={() => setV({ ...v, source: "link" })} /> Link to a file elsewhere
          </label>
        </div>
        {v.source === "file" ? (
          <Field
            label="File"
            hint={material?.fileName && !file ? `Keep the current file (${material.fileName}) or choose a new one. PDF, PNG or JPEG, up to ${maxMb} MB.` : `PDF, PNG or JPEG, up to ${maxMb} MB.`}
            error={tooBig ? `This file is over ${maxMb} MB. Compress it, or upload it elsewhere and use a link.` : undefined}
          >
            <input type="file" accept="application/pdf,image/png,image/jpeg,.pdf,.png,.jpg,.jpeg" required={needsSource} onChange={(e) => setFile(e.target.files?.[0] ?? null)} className="block w-full text-sm file:mr-3 file:rounded-lg file:border file:border-border file:bg-surface-muted file:px-3 file:py-1.5 file:text-sm file:font-medium hover:file:bg-surface" />
          </Field>
        ) : (
          <Field label="Link" hint="An https link, e.g. a Google Drive file shared with “Anyone with the link”.">
            <Input type="url" required={needsSource} placeholder="https://" value={v.externalUrl} onChange={set("externalUrl")} />
          </Field>
        )}
      </fieldset>
      <div className="flex justify-end gap-2">
        <Button variant="secondary" onClick={onDone}>
          Cancel
        </Button>
        <Button type="submit" loading={saving} disabled={tooBig}>
          {material ? "Save" : "Add material"}
        </Button>
      </div>
    </form>
  );
}

/** Free study material for students: upload PDFs or images, or link to files kept elsewhere. */
export function MaterialsAdmin() {
  const list = useQuery({ queryKey: key, queryFn: () => unwrap(api.GET("/api/v1/admin/materials")) });
  const [editing, setEditing] = useState<Material | "new" | null>(null);
  const [deleting, setDeleting] = useState<Material | null>(null);
  const remove = useAction((id: string) => unwrap(api.DELETE("/api/v1/admin/materials/{id}", { params: { path: { id } } })), {
    invalidate: [[...key]],
    success: "Material deleted",
    onSuccess: () => setDeleting(null),
  });
  const maxMb = list.data?.maxUploadMegabytes ?? 20;

  return (
    <>
      <PageHeader
        title="Free material"
        description={list.data ? `Downloads for students (free with an account). Files stored: ${size(list.data.storedBytes)}.` : "Downloads for students (free with an account)."}
        actions={<Button onClick={() => setEditing("new")}>Add material</Button>}
      />
      <Card>
        {list.isPending ? (
          <SkeletonRows />
        ) : list.isError ? (
          <ErrorState error={list.error} onRetry={() => list.refetch()} />
        ) : list.data.items.length === 0 ? (
          <EmptyState title="No material yet" action={<Button onClick={() => setEditing("new")}>Add material</Button>}>
            Upload notes, formula sheets, mind maps or past papers.
          </EmptyState>
        ) : (
          <Table>
            <thead>
              <tr>
                <Th>Title</Th>
                <Th>Type</Th>
                <Th>For</Th>
                <Th>Source</Th>
                <Th className="text-right">Downloads</Th>
                <Th>Status</Th>
                <Th />
              </tr>
            </thead>
            <tbody>
              {list.data.items.map((m) => (
                <tr key={m.id}>
                  <Td className="font-medium">{m.titleEn}</Td>
                  <Td>{typeLabel(m.type)}</Td>
                  <Td>
                    {m.subject ?? "General"} · {m.classLevel ? `Class ${m.classLevel}` : "11 & 12"}
                  </Td>
                  <Td>
                    {m.format === "Link" ? (
                      <a href={m.externalUrl ?? "#"} target="_blank" rel="noreferrer" className="text-primary hover:underline">
                        Link
                      </a>
                    ) : (
                      `${m.format} · ${size(m.sizeBytes)}`
                    )}
                  </Td>
                  <Td className="text-right tabular-nums">{m.downloads}</Td>
                  <Td>
                    <StatusBadge status={m.status} />
                  </Td>
                  <Td className="whitespace-nowrap text-right">
                    <Button size="sm" variant="secondary" onClick={() => setEditing(m)} aria-label={`Edit: ${m.titleEn}`}>
                      Edit
                    </Button>{" "}
                    <Button size="sm" variant="secondary" onClick={() => setDeleting(m)} aria-label={`Delete: ${m.titleEn}`}>
                      Delete
                    </Button>
                  </Td>
                </tr>
              ))}
            </tbody>
          </Table>
        )}
      </Card>
      <Modal open={editing !== null} onClose={() => setEditing(null)} title={editing === "new" ? "Add material" : "Edit material"} wide>
        {editing !== null && <MaterialForm material={editing === "new" ? undefined : editing} maxMb={maxMb} onDone={() => setEditing(null)} />}
      </Modal>
      <ConfirmDialog
        open={!!deleting}
        title="Delete this material?"
        message={<>“{deleting?.titleEn}” and its file will be removed. Students can no longer download it.</>}
        confirmLabel="Delete"
        danger
        loading={remove.isPending}
        onConfirm={() => deleting && remove.mutate(deleting.id)}
        onCancel={() => setDeleting(null)}
      />
    </>
  );
}
