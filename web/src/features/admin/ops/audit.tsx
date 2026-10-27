"use client";

import { useState } from "react";
import { keepPreviousData, useQuery } from "@tanstack/react-query";
import { api, unwrap } from "@/lib/api/client";
import { formatDateTime } from "@/lib/format";
import { EmptyState, ErrorState, SkeletonRows } from "@/components/ui/feedback";
import { Field, Input, Select } from "@/components/ui/field";
import { Card, PageHeader, Pagination, Table, Td, Th } from "@/components/ui/layout";

const ENTITY_TYPES = ["Course", "Subject", "Chapter", "Topic", "LearningResource", "Question", "Test", "Exam", "User", "SupportTicket", "QuestionReport", "ContentImportJob", "Notification"];

function prettyJson(raw: string | null | undefined) {
  if (!raw) return null;
  try {
    return JSON.stringify(JSON.parse(raw), null, 2);
  } catch {
    return raw;
  }
}

export function AuditLog() {
  const [entityType, setEntityType] = useState("");
  const [action, setAction] = useState("");
  const [actionInput, setActionInput] = useState("");
  const [page, setPage] = useState(1);
  const logs = useQuery({
    queryKey: ["admin", "audit", { entityType, action, page }],
    placeholderData: keepPreviousData,
    queryFn: () => unwrap(api.GET("/api/v1/admin/audit-logs", { params: { query: { entityType: entityType || undefined, action: action || undefined, page } } })),
  });

  return (
    <>
      <PageHeader title="Audit log" description="Every change made through the admin portal, with who made it and when. Read-only." />
      <Card className="mb-4 p-4">
        <div className="grid gap-3 sm:grid-cols-2">
          <Field label="Entity" htmlFor="au-entity">
            <Select id="au-entity" value={entityType} onChange={(e) => { setEntityType(e.target.value); setPage(1); }}>
              <option value="">Any</option>
              {ENTITY_TYPES.map((t) => (
                <option key={t}>{t}</option>
              ))}
            </Select>
          </Field>
          <form
            onSubmit={(e) => {
              e.preventDefault();
              setAction(actionInput.trim());
              setPage(1);
            }}
          >
            <Field label="Action starts with" htmlFor="au-action" hint="e.g. question, course.status, user.role. Press Enter.">
              <Input id="au-action" value={actionInput} onChange={(e) => setActionInput(e.target.value)} />
            </Field>
          </form>
        </div>
      </Card>
      <Card>
        {logs.isPending ? (
          <SkeletonRows />
        ) : logs.isError ? (
          <ErrorState error={logs.error} onRetry={() => logs.refetch()} />
        ) : logs.data.items.length === 0 ? (
          <EmptyState title="No matching entries" />
        ) : (
          <>
            <Table>
              <thead>
                <tr>
                  <Th>When</Th>
                  <Th>Who</Th>
                  <Th>Action</Th>
                  <Th>Entity</Th>
                  <Th>Details</Th>
                </tr>
              </thead>
              <tbody className={logs.isPlaceholderData ? "opacity-60" : ""}>
                {logs.data.items.map((l) => {
                  const data = prettyJson(l.dataJson);
                  return (
                    <tr key={l.id}>
                      <Td className="whitespace-nowrap text-fg-muted">{formatDateTime(l.createdAt)}</Td>
                      <Td>{l.actorName ?? "System"}</Td>
                      <Td className="font-mono text-xs">{l.action}</Td>
                      <Td>
                        {l.entityType}
                        {l.entityId && <div className="font-mono text-xs text-fg-muted">{l.entityId}</div>}
                      </Td>
                      <Td className="max-w-sm">
                        {data && (
                          <details>
                            <summary className="cursor-pointer text-sm text-primary">View</summary>
                            <pre className="mt-1 max-h-60 overflow-auto rounded bg-surface-muted p-2 text-xs">{data}</pre>
                          </details>
                        )}
                      </Td>
                    </tr>
                  );
                })}
              </tbody>
            </Table>
            <Pagination page={logs.data.page} pageSize={logs.data.pageSize} total={logs.data.total} onPage={setPage} />
          </>
        )}
      </Card>
    </>
  );
}
