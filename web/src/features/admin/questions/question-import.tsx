"use client";

import { useQuery, useQueryClient } from "@tanstack/react-query";
import { useRef, useState } from "react";
import { api, rawFetch, unwrap } from "@/lib/api/client";
import type { AdminCourseTree, ImportResult } from "@/lib/api/types";
import { formatDateTime } from "@/lib/format";
import { Button, LinkButton } from "@/components/ui/button";
import { Badge, EmptyState, ErrorState, InlineError, SkeletonRows } from "@/components/ui/feedback";
import { Field } from "@/components/ui/field";
import { Card, PageHeader, Table, Td, Th } from "@/components/ui/layout";
import { useToast } from "@/components/ui/toast";
import { BackLink, CourseSelect, useCourseTree } from "../shared";

const HEADER = ["external_ref", "topic_id", "type", "difficulty", "stem_en", "stem_hi", "option_a", "option_b", "option_c", "option_d", "correct", "explanation_en", "marks", "negative_marks"];

const csvCell = (v: string | number) => {
  const s = String(v);
  return /[",\n\r]/.test(s) ? `"${s.replace(/"/g, '""')}"` : s;
};
const toCsv = (rows: (string | number)[][]) => rows.map((r) => r.map(csvCell).join(",")).join("\r\n") + "\r\n";

function download(name: string, content: string) {
  // Byte-order mark so Excel opens Hindi text as UTF-8.
  const url = URL.createObjectURL(new Blob(["﻿" + content], { type: "text/csv;charset=utf-8" }));
  const a = Object.assign(document.createElement("a"), { href: url, download: name });
  a.click();
  URL.revokeObjectURL(url);
}

function topicsOf(tree: AdminCourseTree) {
  return tree.subjects.flatMap((s) => s.chapters.flatMap((c) => c.topics.map((t) => ({ subject: s.titleEn, chapter: c.titleEn, topic: t.titleEn, id: t.id }))));
}

export function QuestionImport() {
  const qc = useQueryClient();
  const toast = useToast();
  const fileRef = useRef<HTMLInputElement>(null);
  const [courseId, setCourseId] = useState("");
  const [file, setFile] = useState<File | null>(null);
  const [uploading, setUploading] = useState(false);
  const [error, setError] = useState<unknown>(null);
  const [result, setResult] = useState<ImportResult | null>(null);
  const tree = useCourseTree(courseId || null);
  const jobs = useQuery({ queryKey: ["admin", "imports"], queryFn: () => unwrap(api.GET("/api/v1/admin/imports")) });

  const downloadTemplate = () => {
    const topicId = tree.data ? topicsOf(tree.data)[0]?.id ?? "<topic id>" : "<topic id>";
    download(
      "question-import-template.csv",
      toCsv([
        HEADER,
        ["PHY-001", topicId, "SingleCorrect", "Easy", "SI unit of force is:", "बल का SI मात्रक है:", "Newton", "Joule", "Watt", "Pascal", "A", "Force is measured in newtons.", 4, 1],
        ["PHY-002", topicId, "MultipleCorrect", "Hard", "Which are vector quantities?", "", "Velocity", "Speed", "Force", "Mass", "A;C", "Velocity and force have direction.", 4, 2],
        ["PHY-003", topicId, "Numerical", "Medium", "g on Earth in m/s^2 (one decimal)?", "", "", "", "", "", 9.8, "", 4, 0],
        ["PHY-004", topicId, "TrueFalse", "Easy", "Light travels faster than sound.", "", "", "", "", "", "A", "A = True, B = False.", 4, 1],
      ]),
    );
  };

  const downloadTopics = () => {
    if (!tree.data) return;
    download(`topics-${tree.data.course.slug}.csv`, toCsv([["subject", "chapter", "topic", "topic_id"], ...topicsOf(tree.data).map((t) => [t.subject, t.chapter, t.topic, t.id])]));
  };

  const upload = async () => {
    if (!file) return;
    setUploading(true);
    setError(null);
    setResult(null);
    try {
      const form = new FormData();
      form.append("file", file);
      const r = await rawFetch<ImportResult>("/api/v1/admin/questions/import", { method: "POST", body: form });
      setResult(r);
      toast(`Imported ${r.imported} of ${r.totalRows} rows`, r.failed > 0 ? "error" : "success");
      await qc.invalidateQueries({ queryKey: ["admin"] });
      setFile(null);
      if (fileRef.current) fileRef.current.value = "";
    } catch (e) {
      setError(e);
    } finally {
      setUploading(false);
    }
  };

  return (
    <>
      <PageHeader
        back={<BackLink href="/admin/questions">Question bank</BackLink>}
        title="Import questions"
        description="Upload a CSV (UTF-8). Valid rows are imported as drafts; rows with problems are listed so you can fix and re-upload just those."
      />

      <div className="grid gap-6 lg:grid-cols-2">
        <Card title="1. Prepare your file" className="p-0">
          <div className="space-y-4 p-4 text-sm">
            <p>
              Each row is one question. Put the correct answer in <code className="rounded bg-surface-muted px-1">correct</code>: a letter (<code>A</code>) or letters
              separated by semicolons (<code>A;C</code>) for option questions, or a number for numerical/integer questions. Types: SingleCorrect, MultipleCorrect,
              Numerical, Integer, TrueFalse.
            </p>
            <Field label="Course (for topic IDs)" htmlFor="imp-course" hint="Questions are tagged by topic ID. Pick a course to download its topic list.">
              <CourseSelect id="imp-course" value={courseId} onChange={setCourseId} />
            </Field>
            <div className="flex flex-wrap gap-2">
              <Button variant="secondary" size="sm" onClick={downloadTemplate}>
                Download template
              </Button>
              <Button variant="secondary" size="sm" onClick={downloadTopics} disabled={!tree.data}>
                Download topic IDs
              </Button>
            </div>
          </div>
        </Card>

        <Card title="2. Upload" className="p-0">
          <div className="space-y-4 p-4">
            <Field label="CSV file" htmlFor="imp-file" hint="Up to 5 MB. Reference IDs must be unique across the question bank.">
              <input
                ref={fileRef}
                id="imp-file"
                type="file"
                accept=".csv,text/csv"
                onChange={(e) => setFile(e.target.files?.[0] ?? null)}
                className="block w-full text-sm file:mr-3 file:rounded-lg file:border file:border-border file:bg-surface file:px-3 file:py-1.5 file:text-sm"
              />
            </Field>
            <InlineError error={error} />
            <Button onClick={upload} disabled={!file} loading={uploading}>
              Upload and import
            </Button>
          </div>
        </Card>
      </div>

      {result && (
        <Card title="Import result" className="mt-6">
          <div className="flex flex-wrap gap-3 p-4 text-sm">
            <Badge>{result.totalRows} rows</Badge>
            <Badge tone="success">{result.imported} imported as draft</Badge>
            {result.failed > 0 && <Badge tone="danger">{result.failed} need fixing</Badge>}
            {result.imported > 0 && (
              <LinkButton size="sm" variant="secondary" href="/admin/questions?status=Draft">
                Review drafts
              </LinkButton>
            )}
          </div>
          {result.errors.length > 0 && <ErrorTable errors={result.errors} />}
        </Card>
      )}

      <Card title="Recent imports" className="mt-6">
        {jobs.isPending ? (
          <SkeletonRows rows={3} />
        ) : jobs.isError ? (
          <ErrorState error={jobs.error} onRetry={() => jobs.refetch()} />
        ) : jobs.data.length === 0 ? (
          <EmptyState title="No imports yet" />
        ) : (
          <Table>
            <thead>
              <tr>
                <Th>File</Th>
                <Th>When</Th>
                <Th className="text-right">Rows</Th>
                <Th className="text-right">Imported</Th>
                <Th className="text-right">Failed</Th>
              </tr>
            </thead>
            <tbody>
              {jobs.data.map((j) => (
                <tr key={j.id}>
                  <Td>
                    {j.fileName}
                    {j.errors.length > 0 && (
                      <details className="mt-1 text-xs">
                        <summary className="cursor-pointer text-primary">Show {j.errors.length} error(s)</summary>
                        <ul className="mt-1 space-y-0.5 text-fg-muted">
                          {j.errors.map((e) => (
                            <li key={e.row}>
                              Row {e.row}: {e.message}
                            </li>
                          ))}
                        </ul>
                      </details>
                    )}
                  </Td>
                  <Td className="whitespace-nowrap text-fg-muted">{formatDateTime(j.createdAt)}</Td>
                  <Td className="text-right tabular-nums">{j.totalRows}</Td>
                  <Td className="text-right tabular-nums">{j.importedRows}</Td>
                  <Td className={`text-right tabular-nums ${j.failedRows > 0 ? "text-danger" : ""}`}>{j.failedRows}</Td>
                </tr>
              ))}
            </tbody>
          </Table>
        )}
      </Card>
    </>
  );
}

function ErrorTable({ errors }: { errors: { row: number; message: string }[] }) {
  return (
    <Table>
      <thead>
        <tr>
          <Th>Row</Th>
          <Th>Problem</Th>
        </tr>
      </thead>
      <tbody>
        {errors.map((e) => (
          <tr key={e.row}>
            <Td className="tabular-nums">{e.row}</Td>
            <Td className="text-danger">{e.message}</Td>
          </tr>
        ))}
      </tbody>
    </Table>
  );
}
