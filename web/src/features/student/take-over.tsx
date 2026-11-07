"use client";

import { ApiError } from "@/lib/api/client";
import { useT } from "@/lib/i18n";
import { Button } from "@/components/ui/button";
import { Modal } from "@/components/ui/dialog";

/** The other signed-in device, from a 409 "session_active" answer. */
export type OtherDevice = { name: string; lastActiveAt: string | null };

export function otherDevice(e: unknown): OtherDevice | null {
  if (!(e instanceof ApiError) || e.status !== 409 || e.code !== "session_active") return null;
  const d = e.details ?? {};
  return { name: typeof d.deviceName === "string" && d.deviceName ? d.deviceName : "?", lastActiveAt: typeof d.lastActiveAt === "string" ? d.lastActiveAt : null };
}

/** "5 minutes ago" in the student's language. */
export function useAgo() {
  const t = useT();
  return (iso: string | null) => {
    if (!iso) return t("timeJustNow");
    const minutes = Math.max(0, Math.round((Date.now() - new Date(iso).getTime()) / 60_000));
    if (minutes < 1) return t("timeJustNow");
    if (minutes < 60) return t("timeMinutesAgo", { count: minutes });
    const hours = Math.round(minutes / 60);
    return hours < 24 ? t("timeHoursAgo", { count: hours }) : t("timeDaysAgo", { count: Math.round(hours / 24) });
  };
}

/** Asks before signing in here would sign the other device out. */
export function TakeOverDialog({ device, busy, onContinue, onCancel }: { device: OtherDevice | null; busy: boolean; onContinue: () => void; onCancel: () => void }) {
  const t = useT();
  const ago = useAgo();
  return (
    <Modal
      open={!!device}
      onClose={onCancel}
      title={t("sessionActiveTitle")}
      closeLabel={t("cancel")}
      footer={
        <>
          <Button variant="secondary" onClick={onCancel} disabled={busy}>
            {t("cancel")}
          </Button>
          <Button onClick={onContinue} loading={busy}>
            {t("continueHere")}
          </Button>
        </>
      }
    >
      {device && <p>{t("sessionActiveBody", { device: device.name, time: ago(device.lastActiveAt) })}</p>}
    </Modal>
  );
}
