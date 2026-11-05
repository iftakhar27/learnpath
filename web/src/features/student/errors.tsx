"use client";

import { ApiError, errorMessage } from "@/lib/api/client";
import { useT } from "@/lib/i18n";

/** Error text in the student's language where we know the case; otherwise the API's own message. */
export function useErrorText() {
  const t = useT();
  return (e: unknown): string => {
    if (e instanceof ApiError) {
      if (e.status === 0) return t("errorNetwork");
      if (e.code === "api_unavailable" || e.code === "bad_response") return t("serverStarting");
      if (e.code === "api_unreachable") return t("serverUnreachable");
      if (e.code === "otp_send_failed") return t("otpSendFailed");
      if (e.code === "otp_unavailable") return t("otpUnavailable");
      if (e.code === "terms_required") return t("mustAcceptTerms");
      if (e.status >= 500 && e.code.startsWith("http_")) return t("errorGeneric");
    }
    return errorMessage(e);
  };
}

export function StudentError({ error, onRetry }: { error: unknown; onRetry?: () => void }) {
  const t = useT();
  const text = useErrorText();
  if (!error) return null;
  return (
    <div role="alert" className="rounded-lg bg-danger-soft px-3 py-2 text-sm text-danger">
      {text(error)}
      {onRetry && (
        <button type="button" onClick={onRetry} className="ml-2 font-medium underline">
          {t("retry")}
        </button>
      )}
    </div>
  );
}
