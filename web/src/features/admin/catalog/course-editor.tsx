"use client";

import { useParams } from "next/navigation";
import { useState } from "react";
import { api, unwrap } from "@/lib/api/client";
import type { CourseUpsert, PublishStatus } from "@/lib/api/types";
import { formatNumber, formatPaise } from "@/lib/format";
import { can, useSession } from "@/lib/session";
import { LinkButton } from "@/components/ui/button";
import { Badge, ErrorState, PageLoading, StatusBadge } from "@/components/ui/feedback";
import { Card, PageHeader, Tabs } from "@/components/ui/layout";
import { CourseForm } from "./course-form";
import { SyllabusEditor } from "./syllabus";
import { BackLink, StatusActions, useAction, useCourseTree } from "../shared";

export function CourseEditor() {
  const { courseId } = useParams<{ courseId: string }>();
  const { data: user } = useSession();
  const tree = useCourseTree(courseId);
  const [tab, setTab] = useState<"syllabus" | "settings">("syllabus");
  const treeKey = ["admin", "course-tree", courseId];

  const update = useAction((body: CourseUpsert) => unwrap(api.PUT("/api/v1/admin/courses/{id}", { params: { path: { id: courseId } }, body })), {
    invalidate: [treeKey, ["admin", "courses"]],
    success: "Course saved",
  });
  const setStatus = useAction((status: PublishStatus) => unwrap(api.POST("/api/v1/admin/courses/{id}/status", { params: { path: { id: courseId } }, body: { status } })), {
    invalidate: [treeKey, ["admin", "courses"]],
    success: "Course status updated",
  });

  if (tree.isPending) return <PageLoading />;
  if (tree.isError) return <ErrorState error={tree.error} onRetry={() => tree.refetch()} />;
  const course = tree.data.course;
  const topics = tree.data.subjects.flatMap((s) => s.chapters).flatMap((c) => c.topics);
  const resources = topics.flatMap((t) => t.resources);
  const unpublished = resources.filter((r) => r.status !== "Published").length;

  return (
    <>
      <PageHeader
        back={<BackLink href="/admin/catalog">Courses</BackLink>}
        title={
          <span className="flex flex-wrap items-center gap-2">
            {course.titleEn} <StatusBadge status={course.status} />
          </span>
        }
        description={
          <>
            {course.isFree ? "Free" : `${formatPaise(course.priceInPaise)} · ${course.accessDays ? `${course.accessDays} days access` : "lifetime access"}`} ·{" "}
            {formatNumber(topics.length)} topics · {formatNumber(resources.length)} videos/notes · {formatNumber(course.enrollments)} enrolled
          </>
        }
        actions={
          <>
            <LinkButton size="sm" variant="secondary" href={`/admin/questions?courseId=${course.id}`}>
              Questions
            </LinkButton>
            <LinkButton size="sm" variant="secondary" href={`/admin/tests?courseId=${course.id}`}>
              Tests
            </LinkButton>
            {can.publishContent(user?.roles) && <StatusActions status={course.status} noun="course" pending={setStatus.isPending} onChange={(s) => setStatus.mutate(s)} />}
          </>
        }
      />
      {course.status === "Published" && unpublished > 0 && (
        <p className="mb-4 rounded-lg bg-warning-soft px-3 py-2 text-sm text-warning">
          {unpublished} video/notes item{unpublished === 1 ? " is" : "s are"} still in draft and hidden from students.
        </p>
      )}
      <Tabs
        value={tab}
        onChange={setTab}
        tabs={[
          { value: "syllabus", label: "Syllabus" },
          { value: "settings", label: <>Settings {course.isFree && <Badge tone="primary">Free</Badge>}</> },
        ]}
      />
      {tab === "syllabus" ? (
        <SyllabusEditor tree={tree.data} />
      ) : (
        <Card className="p-4 sm:p-6">
          <CourseForm key={course.updatedAt} course={course} submitLabel="Save changes" pending={update.isPending} onSubmit={(body) => update.mutate(body)} />
        </Card>
      )}
    </>
  );
}
