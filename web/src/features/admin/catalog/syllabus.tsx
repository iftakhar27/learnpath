"use client";

import Link from "next/link";
import { useState, type ReactNode } from "react";
import { useForm } from "react-hook-form";
import { api, unwrap } from "@/lib/api/client";
import type { AdminChapter, AdminCourseTree, AdminResource, AdminSubject, AdminTopic, NodeUpsert, PublishStatus, ResourceType, ResourceUpsert } from "@/lib/api/types";
import { blankToNull } from "@/lib/format";
import { can, useSession } from "@/lib/session";
import { Button } from "@/components/ui/button";
import { ConfirmDialog, Modal } from "@/components/ui/dialog";
import { Badge, EmptyState, StatusBadge } from "@/components/ui/feedback";
import { BilingualRow, Checkbox, Field, Input, Select, Textarea } from "@/components/ui/field";
import { useAction } from "../shared";

type Kind = "subject" | "chapter" | "topic";
type AnyNode = AdminSubject | AdminChapter | AdminTopic;

// Full PUT bodies: the API replaces every field, so reorders must resend the rest unchanged.
const nodeBody = (kind: Kind, n: AnyNode, sortOrder: number): NodeUpsert => {
  if (kind === "subject") {
    const s = n as AdminSubject;
    return { parentId: s.courseId, titleEn: s.titleEn, titleHi: s.titleHi, sortOrder };
  }
  if (kind === "chapter") {
    const c = n as AdminChapter;
    return { parentId: c.subjectId, titleEn: c.titleEn, titleHi: c.titleHi, sortOrder, unitTitleEn: c.unitTitleEn };
  }
  const t = n as AdminTopic;
  return { parentId: t.chapterId, titleEn: t.titleEn, titleHi: t.titleHi, sortOrder, objectivesEn: t.objectivesEn, objectivesHi: t.objectivesHi, estimatedMinutes: t.estimatedMinutes };
};

const resourceBody = (r: AdminResource, sortOrder: number): ResourceUpsert => ({
  topicId: r.topicId, type: r.type, titleEn: r.titleEn, titleHi: r.titleHi, language: r.language, sortOrder, isFreePreview: r.isFreePreview,
  videoProviderId: r.videoProviderId, durationSeconds: r.durationSeconds, documentStorageKey: r.documentStorageKey, allowDownload: r.allowDownload,
});

function putNode(kind: Kind, id: string, body: NodeUpsert) {
  const params = { path: { id } };
  if (kind === "subject") return unwrap(api.PUT("/api/v1/admin/subjects/{id}", { params, body }));
  if (kind === "chapter") return unwrap(api.PUT("/api/v1/admin/chapters/{id}", { params, body }));
  return unwrap(api.PUT("/api/v1/admin/topics/{id}", { params, body }));
}

function postNode(kind: Kind, body: NodeUpsert) {
  if (kind === "subject") return unwrap(api.POST("/api/v1/admin/subjects", { body }));
  if (kind === "chapter") return unwrap(api.POST("/api/v1/admin/chapters", { body }));
  return unwrap(api.POST("/api/v1/admin/topics", { body }));
}

function deleteNode(kind: Kind | "resource", id: string) {
  const params = { path: { id } };
  if (kind === "subject") return unwrap(api.DELETE("/api/v1/admin/subjects/{id}", { params }));
  if (kind === "chapter") return unwrap(api.DELETE("/api/v1/admin/chapters/{id}", { params }));
  if (kind === "topic") return unwrap(api.DELETE("/api/v1/admin/topics/{id}", { params }));
  return unwrap(api.DELETE("/api/v1/admin/resources/{id}", { params }));
}

/** Moves item `index` by `dir` and renumbers sort orders 0..n-1, saving only the items whose order changed. */
async function reorder<T extends { id: string; sortOrder: number }>(items: T[], index: number, dir: -1 | 1, save: (item: T, sortOrder: number) => Promise<unknown>) {
  const target = index + dir;
  if (target < 0 || target >= items.length) return;
  const next = [...items];
  [next[index], next[target]] = [next[target], next[index]];
  await Promise.all(next.map((item, i) => (item.sortOrder !== i ? save(item, i) : null)));
}

type ModalState =
  | { type: "node"; kind: Kind; parentId: string; node?: AnyNode; nextOrder: number }
  | { type: "resource"; topicId: string; resource?: AdminResource; nextOrder: number }
  | { type: "delete"; kind: Kind | "resource"; id: string; title: string }
  | null;

export function SyllabusEditor({ tree }: { tree: AdminCourseTree }) {
  const { data: user } = useSession();
  const canPublish = can.publishContent(user?.roles);
  const [modal, setModal] = useState<ModalState>(null);
  const treeKey = ["admin", "course-tree", tree.course.id];

  const move = useAction(({ run }: { run: () => Promise<unknown> }) => run(), { invalidate: [treeKey] });
  const remove = useAction(({ kind, id }: { kind: Kind | "resource"; id: string }) => deleteNode(kind, id), {
    invalidate: [treeKey, ["admin", "courses"]],
    success: "Deleted",
    onSuccess: () => setModal(null),
  });
  const setResourceStatus = useAction(({ id, status }: { id: string; status: PublishStatus }) =>
    unwrap(api.POST("/api/v1/admin/resources/{id}/status", { params: { path: { id } }, body: { status } })),
    { invalidate: [treeKey], success: "Resource updated" },
  );

  const nodeMoves = (kind: Kind, siblings: AnyNode[], i: number) => (
    <MoveButtons
      first={i === 0}
      last={i === siblings.length - 1}
      disabled={move.isPending}
      onMove={(dir) => move.mutate({ run: () => reorder(siblings, i, dir, (n, order) => putNode(kind, n.id, nodeBody(kind, n, order))) })}
    />
  );

  return (
    <div className="space-y-4">
      <div className="flex justify-end">
        <Button onClick={() => setModal({ type: "node", kind: "subject", parentId: tree.course.id, nextOrder: tree.subjects.length })}>Add subject</Button>
      </div>
      {tree.subjects.length === 0 && (
        <EmptyState title="No subjects yet">Start with a subject (e.g. Physics), then add chapters and topics inside it.</EmptyState>
      )}

      {tree.subjects.map((s, si) => (
        <section key={s.id} className="rounded-xl border border-border bg-surface" aria-label={`Subject ${s.titleEn}`}>
          <NodeHeader
            level="subject"
            title={s.titleEn}
            titleHi={s.titleHi}
            meta={`${s.chapters.length} chapter${s.chapters.length === 1 ? "" : "s"}`}
            actions={
              <>
                {nodeMoves("subject", tree.subjects, si)}
                <Button size="sm" variant="ghost" onClick={() => setModal({ type: "node", kind: "chapter", parentId: s.id, nextOrder: s.chapters.length })}>
                  Add chapter
                </Button>
                <Button size="sm" variant="ghost" onClick={() => setModal({ type: "node", kind: "subject", parentId: s.courseId, node: s, nextOrder: s.sortOrder })}>
                  Edit
                </Button>
                <Button size="sm" variant="ghost" onClick={() => setModal({ type: "delete", kind: "subject", id: s.id, title: s.titleEn })}>
                  Delete
                </Button>
              </>
            }
          />
          <div className="space-y-3 p-3">
            {s.chapters.length === 0 && <p className="px-2 text-sm text-fg-muted">No chapters yet.</p>}
            {s.chapters.map((c, ci) => (
              <div key={c.id} className="rounded-lg border border-border" aria-label={`Chapter ${c.titleEn}`} role="group">
                <NodeHeader
                  level="chapter"
                  title={c.titleEn}
                  titleHi={c.titleHi}
                  eyebrow={c.unitTitleEn}
                  meta={`${c.topics.length} topic${c.topics.length === 1 ? "" : "s"}`}
                  actions={
                    <>
                      {nodeMoves("chapter", s.chapters, ci)}
                      <Button size="sm" variant="ghost" onClick={() => setModal({ type: "node", kind: "topic", parentId: c.id, nextOrder: c.topics.length })}>
                        Add topic
                      </Button>
                      <Button size="sm" variant="ghost" onClick={() => setModal({ type: "node", kind: "chapter", parentId: c.subjectId, node: c, nextOrder: c.sortOrder })}>
                        Edit
                      </Button>
                      <Button size="sm" variant="ghost" onClick={() => setModal({ type: "delete", kind: "chapter", id: c.id, title: c.titleEn })}>
                        Delete
                      </Button>
                    </>
                  }
                />
                <ul className="divide-y divide-border">
                  {c.topics.length === 0 && <li className="px-4 py-3 text-sm text-fg-muted">No topics yet.</li>}
                  {c.topics.map((t, ti) => (
                    <li key={t.id} className="px-4 py-3" aria-label={`Topic ${t.titleEn}`}>
                      <div className="flex flex-wrap items-start justify-between gap-2">
                        <div className="min-w-0">
                          <p className="font-medium">{t.titleEn}</p>
                          {t.titleHi && <p className="text-xs text-fg-muted" lang="hi">{t.titleHi}</p>}
                          <p className="mt-0.5 text-xs text-fg-muted">
                            {t.estimatedMinutes > 0 && `${t.estimatedMinutes} min · `}
                            <Link href={`/admin/questions?courseId=${tree.course.id}&chapterId=${c.id}&topicId=${t.id}`} className="text-primary hover:underline">
                              {t.questionCount} question{t.questionCount === 1 ? "" : "s"}
                            </Link>
                          </p>
                        </div>
                        <div className="flex flex-wrap items-center gap-1">
                          {nodeMoves("topic", c.topics, ti)}
                          <Button size="sm" variant="ghost" onClick={() => setModal({ type: "resource", topicId: t.id, nextOrder: t.resources.length })}>
                            Add video / notes
                          </Button>
                          <Button size="sm" variant="ghost" onClick={() => setModal({ type: "node", kind: "topic", parentId: t.chapterId, node: t, nextOrder: t.sortOrder })}>
                            Edit
                          </Button>
                          <Button size="sm" variant="ghost" onClick={() => setModal({ type: "delete", kind: "topic", id: t.id, title: t.titleEn })}>
                            Delete
                          </Button>
                        </div>
                      </div>
                      {t.resources.length > 0 && (
                        <ul className="mt-2 space-y-1">
                          {t.resources.map((r, ri) => (
                            <li key={r.id} className="flex flex-wrap items-center justify-between gap-2 rounded-lg bg-surface-muted px-3 py-2 text-sm">
                              <div className="flex min-w-0 flex-wrap items-center gap-2">
                                <Badge tone={r.type === "Video" ? "primary" : "neutral"}>{r.type === "Video" ? "Video" : "Notes"}</Badge>
                                <span className="truncate">{r.titleEn}</span>
                                {r.type === "Video" && r.durationSeconds != null && <span className="text-xs text-fg-muted">{formatDuration(r.durationSeconds)}</span>}
                                {r.isFreePreview && <Badge tone="success">Free preview</Badge>}
                                {r.language === "hi" && <Badge>Hindi</Badge>}
                                <StatusBadge status={r.status} />
                              </div>
                              <div className="flex flex-wrap items-center gap-1">
                                <MoveButtons
                                  first={ri === 0}
                                  last={ri === t.resources.length - 1}
                                  disabled={move.isPending}
                                  onMove={(dir) => move.mutate({ run: () => reorder(t.resources, ri, dir, (x, order) => unwrap(api.PUT("/api/v1/admin/resources/{id}", { params: { path: { id: x.id } }, body: resourceBody(x, order) }))) })}
                                />
                                {canPublish && (
                                  <Button
                                    size="sm"
                                    variant="ghost"
                                    onClick={() => setResourceStatus.mutate({ id: r.id, status: r.status === "Published" ? "Draft" : "Published" })}
                                  >
                                    {r.status === "Published" ? "Unpublish" : "Publish"}
                                  </Button>
                                )}
                                <Button size="sm" variant="ghost" onClick={() => setModal({ type: "resource", topicId: t.id, resource: r, nextOrder: r.sortOrder })}>
                                  Edit
                                </Button>
                                {r.status === "Draft" && (
                                  <Button size="sm" variant="ghost" onClick={() => setModal({ type: "delete", kind: "resource", id: r.id, title: r.titleEn })}>
                                    Delete
                                  </Button>
                                )}
                              </div>
                            </li>
                          ))}
                        </ul>
                      )}
                    </li>
                  ))}
                </ul>
              </div>
            ))}
          </div>
        </section>
      ))}

      {modal?.type === "node" && <NodeModal state={modal} treeKey={treeKey} onClose={() => setModal(null)} />}
      {modal?.type === "resource" && <ResourceModal state={modal} treeKey={treeKey} onClose={() => setModal(null)} />}
      <ConfirmDialog
        open={modal?.type === "delete"}
        title="Delete?"
        danger
        confirmLabel="Delete"
        loading={remove.isPending}
        message={
          modal?.type === "delete" && (
            <>
              Delete <strong>{modal.title}</strong>? Only empty items and unused draft resources can be deleted; otherwise archive the course instead.
            </>
          )
        }
        onCancel={() => setModal(null)}
        onConfirm={() => modal?.type === "delete" && remove.mutate({ kind: modal.kind, id: modal.id })}
      />
    </div>
  );
}

function NodeHeader({ level, title, titleHi, eyebrow, meta, actions }: { level: "subject" | "chapter"; title: string; titleHi?: string | null; eyebrow?: string | null; meta: string; actions: ReactNode }) {
  return (
    <div className={`flex flex-wrap items-center justify-between gap-2 px-4 py-2.5 ${level === "subject" ? "border-b border-border" : "border-b border-border bg-surface-muted/50"}`}>
      <div className="min-w-0">
        {eyebrow && <p className="text-xs uppercase tracking-wide text-fg-muted">{eyebrow}</p>}
        <p className={level === "subject" ? "text-lg font-semibold" : "font-semibold"}>
          {title} {titleHi && <span className="text-sm font-normal text-fg-muted" lang="hi">· {titleHi}</span>}
        </p>
        <p className="text-xs text-fg-muted">{meta}</p>
      </div>
      <div className="flex flex-wrap items-center gap-1">{actions}</div>
    </div>
  );
}

function MoveButtons({ first, last, disabled, onMove }: { first: boolean; last: boolean; disabled?: boolean; onMove: (dir: -1 | 1) => void }) {
  return (
    <span className="inline-flex">
      <button type="button" aria-label="Move up" disabled={first || disabled} onClick={() => onMove(-1)} className="rounded px-1.5 py-1 text-fg-muted hover:bg-surface-muted disabled:opacity-30">
        ↑
      </button>
      <button type="button" aria-label="Move down" disabled={last || disabled} onClick={() => onMove(1)} className="rounded px-1.5 py-1 text-fg-muted hover:bg-surface-muted disabled:opacity-30">
        ↓
      </button>
    </span>
  );
}

export function formatDuration(seconds: number) {
  const m = Math.floor(seconds / 60);
  const s = seconds % 60;
  return `${m}:${String(s).padStart(2, "0")}`;
}

function parseDuration(value: string): number | null {
  const m = /^(\d{1,3}):([0-5]\d)$/.exec(value.trim());
  return m ? Number(m[1]) * 60 + Number(m[2]) : null;
}

const KIND_LABEL: Record<Kind, string> = { subject: "subject", chapter: "chapter", topic: "topic" };

type NodeValues = { titleEn: string; titleHi: string; unitTitleEn: string; objectivesEn: string; objectivesHi: string; estimatedMinutes: string };

function NodeModal({ state, treeKey, onClose }: { state: Extract<ModalState, { type: "node" }>; treeKey: unknown[]; onClose: () => void }) {
  const { kind, node } = state;
  const chapter = kind === "chapter" ? (node as AdminChapter | undefined) : undefined;
  const topic = kind === "topic" ? (node as AdminTopic | undefined) : undefined;
  const { register, handleSubmit, formState } = useForm<NodeValues>({
    defaultValues: {
      titleEn: node?.titleEn ?? "",
      titleHi: node?.titleHi ?? "",
      unitTitleEn: chapter?.unitTitleEn ?? "",
      objectivesEn: topic?.objectivesEn ?? "",
      objectivesHi: topic?.objectivesHi ?? "",
      estimatedMinutes: String(topic?.estimatedMinutes ?? 30),
    },
  });
  const save = useAction((body: NodeUpsert) => (node ? putNode(kind, node.id, body) : postNode(kind, body)), {
    invalidate: [treeKey, ["admin", "courses"]],
    success: node ? "Saved" : `${KIND_LABEL[kind][0].toUpperCase() + KIND_LABEL[kind].slice(1)} added`,
    onSuccess: onClose,
  });

  return (
    <Modal open onClose={onClose} title={`${node ? "Edit" : "Add"} ${KIND_LABEL[kind]}`} wide={kind === "topic"}>
      <form
        noValidate
        className="space-y-4"
        onSubmit={handleSubmit((v) =>
          save.mutate({
            parentId: state.parentId,
            titleEn: v.titleEn.trim(),
            titleHi: blankToNull(v.titleHi),
            sortOrder: state.nextOrder,
            unitTitleEn: kind === "chapter" ? blankToNull(v.unitTitleEn) : null,
            objectivesEn: kind === "topic" ? blankToNull(v.objectivesEn) : null,
            objectivesHi: kind === "topic" ? blankToNull(v.objectivesHi) : null,
            estimatedMinutes: kind === "topic" ? Number(v.estimatedMinutes) || 0 : 0,
          }),
        )}
      >
        <BilingualRow>
          <Field label="Title (English)" htmlFor="node-title" required error={formState.errors.titleEn?.message}>
            <Input id="node-title" autoFocus {...register("titleEn", { required: "Title is required", maxLength: { value: 200, message: "Max 200 characters" } })} />
          </Field>
          <Field label="Title (Hindi)" htmlFor="node-title-hi">
            <Input id="node-title-hi" lang="hi" {...register("titleHi")} />
          </Field>
        </BilingualRow>
        {kind === "chapter" && (
          <Field label="Unit (optional)" htmlFor="node-unit" hint="Groups chapters, e.g. Mechanics.">
            <Input id="node-unit" {...register("unitTitleEn")} />
          </Field>
        )}
        {kind === "topic" && (
          <>
            <BilingualRow>
              <Field label="Learning objectives (English)" htmlFor="node-obj">
                <Textarea id="node-obj" {...register("objectivesEn")} />
              </Field>
              <Field label="Learning objectives (Hindi)" htmlFor="node-obj-hi">
                <Textarea id="node-obj-hi" lang="hi" {...register("objectivesHi")} />
              </Field>
            </BilingualRow>
            <Field label="Estimated study time (minutes)" htmlFor="node-mins" error={formState.errors.estimatedMinutes?.message}>
              <Input id="node-mins" type="number" min={0} max={1000} {...register("estimatedMinutes", { validate: (v) => (Number(v) >= 0 && Number(v) <= 1000) || "Between 0 and 1000" })} />
            </Field>
          </>
        )}
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

type ResourceValues = {
  type: ResourceType;
  titleEn: string;
  titleHi: string;
  language: string;
  isFreePreview: boolean;
  allowDownload: boolean;
  videoProviderId: string;
  duration: string;
  documentStorageKey: string;
};

function ResourceModal({ state, treeKey, onClose }: { state: Extract<ModalState, { type: "resource" }>; treeKey: unknown[]; onClose: () => void }) {
  const r = state.resource;
  const { register, handleSubmit, watch, formState } = useForm<ResourceValues>({
    defaultValues: {
      type: r?.type ?? "Video",
      titleEn: r?.titleEn ?? "",
      titleHi: r?.titleHi ?? "",
      language: r?.language ?? "en",
      isFreePreview: r?.isFreePreview ?? false,
      allowDownload: r?.allowDownload ?? false,
      videoProviderId: r?.videoProviderId ?? "",
      duration: r?.durationSeconds != null ? formatDuration(r.durationSeconds) : "",
      documentStorageKey: r?.documentStorageKey ?? "",
    },
  });
  const type = watch("type");
  const errors = formState.errors;
  const save = useAction(
    (body: ResourceUpsert) => (r ? unwrap(api.PUT("/api/v1/admin/resources/{id}", { params: { path: { id: r.id } }, body })) : unwrap(api.POST("/api/v1/admin/resources", { body }))),
    { invalidate: [treeKey], success: r ? "Resource saved" : "Resource added as draft", onSuccess: onClose },
  );

  return (
    <Modal open onClose={onClose} title={r ? "Edit resource" : "Add video or notes"} wide>
      <form
        noValidate
        className="space-y-4"
        onSubmit={handleSubmit((v) =>
          save.mutate({
            topicId: state.topicId,
            type: v.type,
            titleEn: v.titleEn.trim(),
            titleHi: blankToNull(v.titleHi),
            language: v.language,
            sortOrder: state.nextOrder,
            isFreePreview: v.isFreePreview,
            allowDownload: v.type === "Document" && v.allowDownload,
            videoProviderId: v.type === "Video" ? v.videoProviderId.trim() : null,
            durationSeconds: v.type === "Video" ? parseDuration(v.duration) : null,
            documentStorageKey: v.type === "Document" ? v.documentStorageKey.trim() : null,
          }),
        )}
      >
        <div className="grid gap-4 sm:grid-cols-2">
          <Field label="Type" htmlFor="res-type">
            <Select id="res-type" {...register("type")}>
              <option value="Video">Video lesson</option>
              <option value="Document">Notes (PDF)</option>
            </Select>
          </Field>
          <Field label="Content language" htmlFor="res-lang">
            <Select id="res-lang" {...register("language")}>
              <option value="en">English</option>
              <option value="hi">Hindi</option>
            </Select>
          </Field>
        </div>
        <BilingualRow>
          <Field label="Title (English)" htmlFor="res-title" required error={errors.titleEn?.message}>
            <Input id="res-title" {...register("titleEn", { required: "Title is required" })} />
          </Field>
          <Field label="Title (Hindi)" htmlFor="res-title-hi">
            <Input id="res-title-hi" lang="hi" {...register("titleHi")} />
          </Field>
        </BilingualRow>
        {type === "Video" ? (
          <div className="grid gap-4 sm:grid-cols-2">
            <Field label="Video ID at the streaming provider" htmlFor="res-vid" required error={errors.videoProviderId?.message} hint="Upload the video to your provider (e.g. Bunny Stream) and paste its ID.">
              <Input id="res-vid" {...register("videoProviderId", { validate: (v, all) => all.type !== "Video" || !!v.trim() || "Video ID is required" })} />
            </Field>
            <Field label="Duration (mm:ss)" htmlFor="res-dur" required error={errors.duration?.message} hint="Used for progress and study-plan time.">
              <Input id="res-dur" placeholder="12:30" {...register("duration", { validate: (v, all) => all.type !== "Video" || parseDuration(v) !== null || "Use minutes:seconds, e.g. 12:30" })} />
            </Field>
          </div>
        ) : (
          <>
            <Field label="Storage key" htmlFor="res-doc" required error={errors.documentStorageKey?.message} hint="Path of the PDF in object storage, e.g. neet/physics/friction-notes.pdf">
              <Input id="res-doc" {...register("documentStorageKey", { validate: (v, all) => all.type !== "Document" || !!v.trim() || "Storage key is required" })} />
            </Field>
            <Checkbox label="Allow download" hint="Otherwise students can only view it in the app." {...register("allowDownload")} />
          </>
        )}
        <Checkbox label="Free preview" hint="Students who haven't bought the course can open this." {...register("isFreePreview")} />
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
