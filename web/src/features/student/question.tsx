"use client";

import { useState } from "react";
import { useT } from "@/lib/i18n";
import { Icon } from "@/components/ui/icons";

export type Answer = { selectedOptionIds?: string[] | null; numericValue?: number | null };
export type QuestionOption = { id: string; text: string };

export const isOptionType = (type: string) => type === "SingleCorrect" || type === "MultipleCorrect" || type === "TrueFalse";
export const isAnswered = (a: Answer | null | undefined) => !!a && ((a.selectedOptionIds?.length ?? 0) > 0 || a.numericValue != null);
const LETTERS = "ABCDEFGHIJ";

/** How options look once the answer is known (practice feedback and review). */
export type OptionFeedback = { correctOptionIds: string[]; selectedOptionIds: string[] };

/**
 * Renders a question and collects the answer. Option questions use radio/checkbox semantics; numerical ones a
 * text field (integers only for `Integer`). `onChange(answer, typing)` — `typing` lets callers debounce saves.
 * `label` names the answer group for screen readers (pages show the question number themselves).
 */
export function QuestionView({
  type,
  stem,
  imageUrl,
  options,
  answer,
  onChange,
  disabled,
  feedback,
  label,
}: {
  type: string;
  stem: string;
  imageUrl?: string | null;
  options: QuestionOption[];
  answer: Answer | null;
  onChange?: (answer: Answer | null, typing?: boolean) => void;
  disabled?: boolean;
  feedback?: OptionFeedback;
  label?: string;
}) {
  const t = useT();
  const multi = type === "MultipleCorrect";
  const selected = answer?.selectedOptionIds ?? [];
  const locked = disabled || !!feedback || !onChange;

  const toggle = (id: string) => {
    if (locked) return;
    const next = multi ? (selected.includes(id) ? selected.filter((x) => x !== id) : [...selected, id]) : [id];
    onChange!(next.length ? { selectedOptionIds: next } : null);
  };

  return (
    <div className="space-y-4">
      <p className="whitespace-pre-line text-lg leading-relaxed">{stem}</p>
      {imageUrl && (
        // eslint-disable-next-line @next/next/no-img-element -- question images come from the content CDN at arbitrary sizes
        <img src={imageUrl} alt="" className="max-h-80 rounded-xl border border-border" />
      )}
      {isOptionType(type) ? (
        <div role={multi ? "group" : "radiogroup"} aria-label={label ?? stem} className="space-y-2">
          {multi && !feedback && <p className="text-sm font-medium text-fg-muted">{t("selectAllThatApply")}</p>}
          {options.map((o, i) => {
            const isSel = feedback ? feedback.selectedOptionIds.includes(o.id) : selected.includes(o.id);
            const isCorrect = feedback?.correctOptionIds.includes(o.id);
            const tone = feedback
              ? isCorrect
                ? "border-success bg-success-soft"
                : isSel
                  ? "border-danger bg-danger-soft"
                  : "border-border"
              : isSel
                ? "border-primary bg-primary-soft"
                : "border-border hover:bg-surface-muted";
            return (
              <button
                key={o.id}
                type="button"
                role={multi ? "checkbox" : "radio"}
                aria-checked={isSel}
                aria-disabled={locked || undefined}
                onClick={() => toggle(o.id)}
                className={`flex w-full items-start gap-3 rounded-xl border p-3 text-left ${tone} ${locked ? "cursor-default" : ""}`}
              >
                <span
                  className={`flex size-7 shrink-0 items-center justify-center rounded-full border text-sm font-semibold ${isSel && !feedback ? "border-primary bg-primary text-primary-fg" : "border-border"}`}
                  aria-hidden
                >
                  {LETTERS[i]}
                </span>
                <span className="min-w-0 flex-1 pt-0.5">
                  <span className="sr-only">{LETTERS[i]}. </span>
                  {o.text}
                </span>
                {feedback && isCorrect && <Icon name="check" className="shrink-0 text-success" aria-label={t("correct")} role="img" />}
                {feedback && isSel && !isCorrect && <Icon name="x" className="shrink-0 text-danger" aria-label={t("incorrect")} role="img" />}
              </button>
            );
          })}
        </div>
      ) : (
        <NumericAnswer integer={type === "Integer"} value={answer?.numericValue ?? null} disabled={locked} onChange={(v) => onChange?.(v == null ? null : { numericValue: v }, true)} />
      )}
    </div>
  );
}

function NumericAnswer({ integer, value, disabled, onChange }: { integer: boolean; value: number | null; disabled: boolean; onChange: (v: number | null) => void }) {
  const t = useT();
  const [text, setText] = useState(value == null ? "" : String(value));
  const pattern = integer ? /^-?\d*$/ : /^-?\d*\.?\d*$/;
  return (
    <label className="block max-w-xs">
      <span className="mb-1 block text-sm font-medium">{t("enterNumber")}</span>
      <input
        inputMode={integer ? "numeric" : "decimal"}
        value={text}
        disabled={disabled}
        onChange={(e) => {
          const v = e.target.value.trim();
          if (!pattern.test(v)) return;
          setText(v);
          const n = v === "" || v === "-" || v === "." || v === "-." ? null : Number(v);
          onChange(n != null && Number.isFinite(n) ? n : null);
        }}
        className="h-12 w-full rounded-lg border border-border bg-surface px-3 text-lg tabular-nums disabled:opacity-70"
      />
    </label>
  );
}
