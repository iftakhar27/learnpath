import { forwardRef, useId, type ComponentProps, type ReactNode } from "react";

/** Default full width unless the caller sets its own width (w-20, w-48, …). */
const width = (className: string) => (/(^|\s)w-/.test(className) ? "" : "w-full");
const control =
  "rounded-lg border border-border bg-surface px-3 text-sm text-fg placeholder:text-fg-muted/70 disabled:opacity-60 aria-[invalid=true]:border-danger";

/**
 * Label + control + hint/error. The label wraps the control instead of using `for`/`id`: Next.js keeps
 * previously visited pages in the DOM (hidden), so the same ids can exist twice and an id-based label could
 * point at the hidden copy. `htmlFor` is accepted for the control's id but not used for labelling.
 */
export function Field({
  label,
  hint,
  error,
  children,
  required,
}: {
  label: string;
  hint?: ReactNode;
  error?: string;
  children: ReactNode;
  htmlFor?: string;
  required?: boolean;
}) {
  return (
    <div className="space-y-1">
      <label className="block space-y-1">
        <span className="block text-sm font-medium">
          {label}
          {required && <span className="text-danger"> *</span>}
        </span>
        {children}
      </label>
      {error ? (
        <p className="text-xs text-danger" role="alert">
          {error}
        </p>
      ) : (
        hint && <p className="text-xs text-fg-muted">{hint}</p>
      )}
    </div>
  );
}

export const Input = forwardRef<HTMLInputElement, ComponentProps<"input">>(function Input({ className = "", ...rest }, ref) {
  return <input ref={ref} className={`${control} ${width(className)} h-10 ${className}`} {...rest} />;
});

export const Textarea = forwardRef<HTMLTextAreaElement, ComponentProps<"textarea">>(function Textarea({ className = "", rows = 3, ...rest }, ref) {
  return <textarea ref={ref} rows={rows} className={`${control} ${width(className)} py-2 ${className}`} {...rest} />;
});

export const Select = forwardRef<HTMLSelectElement, ComponentProps<"select">>(function Select({ className = "", children, ...rest }, ref) {
  return (
    <select ref={ref} className={`${control} ${width(className)} h-10 ${className}`} {...rest}>
      {children}
    </select>
  );
});

export const Checkbox = forwardRef<HTMLInputElement, Omit<ComponentProps<"input">, "type"> & { label: ReactNode; hint?: ReactNode }>(
  function Checkbox({ label, hint, id, className = "", ...rest }, ref) {
    const auto = useId();
    const inputId = id ?? auto;
    return (
      <div className={`flex items-start gap-2 ${className}`}>
        <input ref={ref} id={inputId} type="checkbox" className="mt-0.5 h-4 w-4 rounded border-border accent-[var(--primary)]" {...rest} />
        <label htmlFor={inputId} className="text-sm">
          {label}
          {hint && <span className="block text-xs text-fg-muted">{hint}</span>}
        </label>
      </div>
    );
  },
);

/** Pairs an English and an optional Hindi input side by side. */
export function BilingualRow({ children }: { children: ReactNode }) {
  return <div className="grid gap-4 md:grid-cols-2">{children}</div>;
}
