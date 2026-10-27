import type { ReactNode } from "react";
import { errorMessage } from "@/lib/api/client";
import type { PublishStatus } from "@/lib/api/types";

export function Spinner({ small, label = "Loading" }: { small?: boolean; label?: string }) {
  return (
    <span
      role="status"
      aria-label={label}
      className={`inline-block animate-spin rounded-full border-2 border-current border-t-transparent ${small ? "h-4 w-4" : "h-6 w-6"}`}
    />
  );
}

export function PageLoading() {
  return (
    <div className="space-y-3" aria-busy="true">
      <div className="h-8 w-56 animate-pulse rounded bg-surface-muted" />
      <div className="h-4 w-80 animate-pulse rounded bg-surface-muted" />
      <div className="mt-6 h-64 animate-pulse rounded-xl bg-surface-muted" />
    </div>
  );
}

export function SkeletonRows({ rows = 5 }: { rows?: number }) {
  return (
    <div className="space-y-2 p-4" aria-busy="true">
      {Array.from({ length: rows }, (_, i) => (
        <div key={i} className="h-9 animate-pulse rounded bg-surface-muted" />
      ))}
    </div>
  );
}

export function EmptyState({ title, children, action }: { title: string; children?: ReactNode; action?: ReactNode }) {
  return (
    <div className="flex flex-col items-center gap-2 px-6 py-12 text-center">
      <p className="font-medium">{title}</p>
      {children && <p className="max-w-md text-sm text-fg-muted">{children}</p>}
      {action && <div className="mt-2">{action}</div>}
    </div>
  );
}

export function ErrorState({ error, onRetry }: { error: unknown; onRetry?: () => void }) {
  return (
    <div role="alert" className="flex flex-col items-center gap-2 px-6 py-10 text-center">
      <p className="font-medium text-danger">Couldn&apos;t load this</p>
      <p className="max-w-md text-sm text-fg-muted">{errorMessage(error)}</p>
      {onRetry && (
        <button type="button" onClick={onRetry} className="mt-1 text-sm font-medium text-primary hover:underline">
          Try again
        </button>
      )}
    </div>
  );
}

export function InlineError({ error }: { error: unknown }) {
  if (!error) return null;
  return (
    <p role="alert" className="rounded-lg bg-danger-soft px-3 py-2 text-sm text-danger">
      {errorMessage(error)}
    </p>
  );
}

type Tone = "neutral" | "primary" | "success" | "warning" | "danger";
const tones: Record<Tone, string> = {
  neutral: "bg-surface-muted text-fg-muted",
  primary: "bg-primary-soft text-primary",
  success: "bg-success-soft text-success",
  warning: "bg-warning-soft text-warning",
  danger: "bg-danger-soft text-danger",
};

export function Badge({ tone = "neutral", children }: { tone?: Tone; children: ReactNode }) {
  return <span className={`inline-flex items-center rounded-full px-2 py-0.5 text-xs font-medium ${tones[tone]}`}>{children}</span>;
}

const statusTone: Record<string, Tone> = {
  Published: "success",
  Draft: "warning",
  Archived: "neutral",
  Active: "success",
  Suspended: "danger",
  DeletionRequested: "warning",
  Deleted: "neutral",
  Paid: "success",
  Created: "neutral",
  Failed: "danger",
  Refunded: "warning",
  Open: "warning",
  InProgress: "primary",
  Resolved: "success",
  Closed: "neutral",
  Fixed: "success",
  Rejected: "neutral",
};

export function StatusBadge({ status }: { status: PublishStatus | string }) {
  return <Badge tone={statusTone[status] ?? "neutral"}>{status.replace(/([a-z])([A-Z])/g, "$1 $2")}</Badge>;
}
