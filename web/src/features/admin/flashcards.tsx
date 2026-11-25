"use client";

import Link from "next/link";
import { useParams } from "next/navigation";
import { useState } from "react";
import { useQuery } from "@tanstack/react-query";
import { api, unwrap } from "@/lib/api/client";
import type { components } from "@/lib/api/schema";
import { Button } from "@/components/ui/button";
import { ConfirmDialog } from "@/components/ui/dialog";
import { Badge, EmptyState, ErrorState, SkeletonRows, StatusBadge } from "@/components/ui/feedback";
import { BilingualRow, Field, Select, Textarea } from "@/components/ui/field";
import { Card, PageHeader, Table, Td, Th } from "@/components/ui/layout";
import { BackLink, useAction } from "./shared";

type AdminCard = components["schemas"]["AdminFlashcardDto"];
type Status = components["schemas"]["PublishStatus"];
type ImportResult = components["schemas"]["FlashcardImportResult"];

const decksKey = ["admin", "flashcards", "decks"] as const;
const cardsKey = (chapterId: string) => ["admin", "flashcards", chapterId] as const;
const SUBJECTS = ["Physics", "Chemistry", "Biology"] as const;

/** Every NCERT chapter with its deck size; open one to write or paste cards. */
export function FlashcardDecksAdmin() {
  const [subject, setSubject] = useState<(typeof SUBJECTS)[number]>("Physics");
  const decks = useQuery({ queryKey: decksKey, queryFn: () => unwrap(api.GET("/api/v1/admin/flashcards/decks")) });
  const rows = decks.data?.filter((d) => d.subject === subject) ?? [];
  const total = decks.data?.reduce((n, d) => n + d.published, 0) ?? 0;

  return (
    <>
      <PageHeader
        title="Flash cards"
        description={`Revision cards for each NCERT chapter, free for every student. ${total} published.`}
        actions={
          <Select aria-label="Subject" value={subject} onChange={(e) => setSubject(e.target.value as (typeof SUBJECTS)[number])}>
            {SUBJECTS.map((s) => (
              <option key={s}>{s}</option>
            ))}
          </Select>
        }
      />
      <Card>
        {decks.isPending ? (
          <SkeletonRows />
        ) : decks.isError ? (
          <ErrorState error={decks.error} onRetry={() => decks.refetch()} />
        ) : (
          <Table>
            <thead>
              <tr>
                <Th>Class</Th>
                <Th>Chapter</Th>
                <Th className="text-right">Published</Th>
                <Th className="text-right">Drafts</Th>
              </tr>
            </thead>
            <tbody>
              {rows.map((d) => (
                <tr key={d.chapterId}>
                  <Td>{d.classLevel}</Td>
                  <Td>
                    <Link href={`/admin/flashcards/${d.chapterId}`} className="font-medium text-primary hover:underline">
                      {d.number}. {d.title}
                    </Link>
                  </Td>
                  <Td className="text-right tabular-nums">{d.published || <span className="text-fg-muted">0</span>}</Td>
                  <Td className="text-right tabular-nums">{d.drafts || <span className="text-fg-muted">0</span>}</Td>
                </tr>
              ))}
            </tbody>
          </Table>
        )}
      </Card>
    </>
  );
}

const EMPTY = { frontEn: "", backEn: "", frontHi: "", backHi: "", status: "Published" as Status };

function CardForm({ initial, onSave, onCancel, saving, submitLabel }: { initial: typeof EMPTY; onSave: (v: typeof EMPTY) => void; onCancel?: () => void; saving: boolean; submitLabel: string }) {
  const [v, setV] = useState(initial);
  const set = (k: keyof typeof EMPTY) => (e: { target: { value: string } }) => setV({ ...v, [k]: e.target.value });
  return (
    <form
      className="space-y-3"
      onSubmit={(e) => {
        e.preventDefault();
        onSave(v);
      }}
    >
      <BilingualRow>
        <Field label="Front (English)" required hint="The prompt: a term, question or formula to recall. Up to 500 characters.">
          <Textarea rows={2} required maxLength={500} value={v.frontEn} onChange={set("frontEn")} />
        </Field>
        <Field label="Front (Hindi)">
          <Textarea rows={2} maxLength={500} value={v.frontHi} onChange={set("frontHi")} lang="hi" />
        </Field>
      </BilingualRow>
      <BilingualRow>
        <Field label="Back (English)" required hint="The answer, short enough to read at a glance. Up to 1000 characters.">
          <Textarea rows={3} required maxLength={1000} value={v.backEn} onChange={set("backEn")} />
        </Field>
        <Field label="Back (Hindi)">
          <Textarea rows={3} maxLength={1000} value={v.backHi} onChange={set("backHi")} lang="hi" />
        </Field>
      </BilingualRow>
      <div className="flex flex-wrap items-end justify-between gap-3">
        <Field label="Status">
          <Select value={v.status} onChange={set("status")}>
            <option value="Published">Published (students see it)</option>
            <option value="Draft">Draft (hidden)</option>
          </Select>
        </Field>
        <div className="flex gap-2">
          {onCancel && (
            <Button variant="secondary" onClick={onCancel}>
              Cancel
            </Button>
          )}
          <Button type="submit" loading={saving}>
            {submitLabel}
          </Button>
        </div>
      </div>
    </form>
  );
}

/** One chapter's deck: add a card, paste many at once, edit or delete. */
export function FlashcardDeckEditor() {
  const { chapterId } = useParams<{ chapterId: string }>();
  const decks = useQuery({ queryKey: decksKey, queryFn: () => unwrap(api.GET("/api/v1/admin/flashcards/decks")) });
  const cards = useQuery({ queryKey: cardsKey(chapterId), queryFn: () => unwrap(api.GET("/api/v1/admin/flashcards", { params: { query: { ncertChapterId: chapterId } } })) });
  const deck = decks.data?.find((d) => d.chapterId === chapterId);
  const [formKey, setFormKey] = useState(0);
  const [editing, setEditing] = useState<string | null>(null);
  const [deleting, setDeleting] = useState<AdminCard | null>(null);
  const [paste, setPaste] = useState("");
  const [result, setResult] = useState<ImportResult | null>(null);
  const invalidate = [cardsKey(chapterId), decksKey];
  const body = (v: typeof EMPTY) => ({ ncertChapterId: chapterId, frontEn: v.frontEn, backEn: v.backEn, frontHi: v.frontHi || null, backHi: v.backHi || null, status: v.status });

  const create = useAction((v: typeof EMPTY) => unwrap(api.POST("/api/v1/admin/flashcards", { body: body(v) })), {
    invalidate,
    success: "Card added",
    onSuccess: () => setFormKey((k) => k + 1),
  });
  const update = useAction((v: { id: string; values: typeof EMPTY }) => unwrap(api.PUT("/api/v1/admin/flashcards/{id}", { params: { path: { id: v.id } }, body: body(v.values) })), {
    invalidate,
    success: "Card saved",
    onSuccess: () => setEditing(null),
  });
  const remove = useAction((id: string) => unwrap(api.DELETE("/api/v1/admin/flashcards/{id}", { params: { path: { id } } })), {
    invalidate,
    success: "Card deleted",
    onSuccess: () => setDeleting(null),
  });
  const importCards = useAction(() => unwrap(api.POST("/api/v1/admin/flashcards/import", { body: { ncertChapterId: chapterId, text: paste, status: "Published" } })), {
    invalidate,
    success: (r) => `${r.added} card${r.added === 1 ? "" : "s"} added`,
    onSuccess: (r) => {
      setResult(r);
      if (r.problems.length === 0) setPaste("");
    },
  });

  return (
    <>
      <PageHeader
        back={<BackLink href="/admin/flashcards">Flash cards</BackLink>}
        title={deck ? `${deck.number}. ${deck.title}` : "Flash cards"}
        description={deck ? `${deck.subject} · Class ${deck.classLevel} · ${deck.published} published, ${deck.drafts} draft` : undefined}
      />
      <div className="grid gap-6 lg:grid-cols-2">
        <Card title="Add a card" className="p-0">
          <div className="p-4">
            <CardForm key={formKey} initial={EMPTY} onSave={(v) => create.mutate(v)} saving={create.isPending} submitLabel="Add card" />
          </div>
        </Card>
        <Card title="Paste many" className="p-0">
          <div className="space-y-3 p-4">
            <Field label="Cards, one per line" hint="front | back — optionally | front in Hindi | back in Hindi. A tab works instead of |. Lines starting with # are ignored. Up to 500 at a time.">
              <Textarea rows={8} value={paste} onChange={(e) => setPaste(e.target.value)} placeholder={"Dimensional formula of force | [M L T⁻²]\nAvogadro constant | 6.022 × 10²³ mol⁻¹"} />
            </Field>
            <div className="flex justify-end">
              <Button onClick={() => importCards.mutate(undefined)} loading={importCards.isPending} disabled={!paste.trim()}>
                Add pasted cards
              </Button>
            </div>
            {result && result.problems.length > 0 && (
              <div role="status" className="rounded-lg bg-warning-soft p-3 text-sm text-warning">
                <p className="font-medium">
                  {result.added} added, {result.problems.length} skipped:
                </p>
                <ul className="mt-1 list-disc pl-5">
                  {result.problems.map((p) => (
                    <li key={p.line}>
                      Line {p.line}: {p.reason}
                    </li>
                  ))}
                </ul>
              </div>
            )}
          </div>
        </Card>
      </div>

      <Card title={`Cards${cards.data ? ` (${cards.data.length})` : ""}`} className="mt-6">
        {cards.isPending ? (
          <SkeletonRows />
        ) : cards.isError ? (
          <ErrorState error={cards.error} onRetry={() => cards.refetch()} />
        ) : cards.data.length === 0 ? (
          <EmptyState title="No cards yet">Add one above, or paste a list.</EmptyState>
        ) : (
          <ol className="divide-y divide-border">
            {cards.data.map((c, i) => (
              <li key={c.id} className="p-4">
                {editing === c.id ? (
                  <CardForm
                    initial={{ frontEn: c.frontEn, backEn: c.backEn, frontHi: c.frontHi ?? "", backHi: c.backHi ?? "", status: c.status }}
                    onSave={(values) => update.mutate({ id: c.id, values })}
                    onCancel={() => setEditing(null)}
                    saving={update.isPending}
                    submitLabel="Save"
                  />
                ) : (
                  <div className="flex flex-wrap items-start gap-3">
                    <span className="w-6 shrink-0 pt-0.5 text-sm text-fg-muted tabular-nums">{i + 1}</span>
                    <div className="min-w-0 flex-1 space-y-1">
                      <p className="font-medium">{c.frontEn}</p>
                      <p className="whitespace-pre-line text-sm text-fg-muted">{c.backEn}</p>
                      {(c.frontHi || c.backHi) && <Badge>Hindi</Badge>}
                    </div>
                    <StatusBadge status={c.status} />
                    <div className="flex gap-2">
                      <Button size="sm" variant="secondary" onClick={() => setEditing(c.id)} aria-label={`Edit: ${c.frontEn}`}>
                        Edit
                      </Button>
                      <Button size="sm" variant="secondary" onClick={() => setDeleting(c)} aria-label={`Delete: ${c.frontEn}`}>
                        Delete
                      </Button>
                    </div>
                  </div>
                )}
              </li>
            ))}
          </ol>
        )}
      </Card>
      <ConfirmDialog
        open={!!deleting}
        title="Delete this card?"
        message={<>“{deleting?.frontEn}” will be removed, along with students&apos; progress on it.</>}
        confirmLabel="Delete"
        danger
        loading={remove.isPending}
        onConfirm={() => deleting && remove.mutate(deleting.id)}
        onCancel={() => setDeleting(null)}
      />
    </>
  );
}
