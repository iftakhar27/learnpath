"use client";

import { useState } from "react";
import { api, unwrap } from "@/lib/api/client";
import { useT } from "@/lib/i18n";
import { Button } from "@/components/ui/button";
import { Modal } from "@/components/ui/dialog";
import { Textarea } from "@/components/ui/field";
import { Icon } from "@/components/ui/icons";
import { useToast } from "@/components/ui/toast";
import { StudentError } from "../errors";
import { AskDoubtButton } from "../doubts/doubts";
import { useBookmark } from "../learn/bookmarks";

const REASONS = [
  ["wrong_answer", "reportReasonWrong"],
  ["typo", "reportReasonTypo"],
  ["unclear", "reportReasonUnclear"],
  ["other", "reportReasonOther"],
] as const;

/** "Save for revision", "Ask a doubt" and "Report a problem" for one question. */
export function QuestionTools({ questionId, stem }: { questionId: string; stem?: string }) {
  const t = useT();
  const toast = useToast();
  const bookmark = useBookmark("Question", questionId);
  const [open, setOpen] = useState(false);
  const [reason, setReason] = useState<(typeof REASONS)[number][0]>("wrong_answer");
  const [details, setDetails] = useState("");
  const [sending, setSending] = useState(false);
  const [error, setError] = useState<unknown>(null);

  const send = async () => {
    setSending(true);
    setError(null);
    try {
      await unwrap(api.POST("/api/v1/questions/{questionId}/report", { params: { path: { questionId } }, body: { reason, details: details.trim() || null } }));
      setOpen(false);
      setDetails("");
      toast(t("reportThanks"));
    } catch (e) {
      setError(e);
    } finally {
      setSending(false);
    }
  };

  return (
    <div className="flex flex-wrap gap-2">
      <button
        type="button"
        onClick={() => bookmark.toggle.mutate()}
        disabled={!bookmark.ready || bookmark.toggle.isPending}
        aria-pressed={bookmark.saved}
        className={`inline-flex items-center gap-1.5 rounded-lg border px-3 py-1.5 text-sm font-medium ${bookmark.saved ? "border-primary bg-primary-soft text-primary" : "border-border hover:bg-surface-muted"}`}
      >
        <Icon name="bookmark" size={16} fill={bookmark.saved ? "currentColor" : "none"} />
        {bookmark.saved ? t("bookmarked") : t("bookmark")}
      </button>
      <AskDoubtButton link={{ questionId }} about={stem} />
      <button type="button" onClick={() => setOpen(true)} className="inline-flex items-center gap-1.5 rounded-lg border border-border px-3 py-1.5 text-sm font-medium hover:bg-surface-muted">
        <Icon name="flag" size={16} />
        {t("reportQuestion")}
      </button>
      <Modal
        open={open}
        onClose={() => setOpen(false)}
        title={t("reportQuestion")}
        closeLabel={t("cancel")}
        footer={
          <>
            <Button variant="secondary" onClick={() => setOpen(false)}>
              {t("cancel")}
            </Button>
            <Button onClick={send} loading={sending}>
              {t("send")}
            </Button>
          </>
        }
      >
        <div className="space-y-3">
          <StudentError error={error} />
          <div role="radiogroup" aria-label={t("reportQuestion")} className="space-y-2">
            {REASONS.map(([value, key]) => (
              <label key={value} className="flex items-center gap-2 text-sm">
                <input type="radio" name="report-reason" className="size-4 accent-[var(--primary)]" checked={reason === value} onChange={() => setReason(value)} />
                {t(key)}
              </label>
            ))}
          </div>
          <label className="block space-y-1">
            <span className="text-sm font-medium">{t("reportDetails")}</span>
            <Textarea value={details} onChange={(e) => setDetails(e.target.value)} maxLength={2000} />
          </label>
        </div>
      </Modal>
    </div>
  );
}
