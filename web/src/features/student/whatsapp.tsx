"use client";

import { useQuery } from "@tanstack/react-query";
import { api, unwrap } from "@/lib/api/client";
import { useT } from "@/lib/i18n";
import { Icon } from "@/components/ui/icons";

const brand = process.env.NEXT_PUBLIC_BRAND_NAME ?? "LearnPath";

/** Server settings: the team's WhatsApp number and channel (null when not set) and the website's address. */
export function useAppConfig() {
  return useQuery({ queryKey: ["app-config"], queryFn: () => unwrap(api.GET("/api/v1/app-config")), staleTime: 10 * 60_000 });
}

/** The website's address for shared links: from the server, else the page's own origin. */
function siteUrl(configured: string | null | undefined) {
  return (configured ?? (typeof window === "undefined" ? "" : window.location.origin)).replace(/\/+$/, "");
}

export const whatsAppChatHref = (number: string, message: string) => `https://wa.me/${number}?text=${encodeURIComponent(message)}`;
export const whatsAppShareHref = (message: string) => `https://wa.me/?text=${encodeURIComponent(message)}`;

const rowClass = "flex items-center gap-3 rounded-lg px-3 py-2.5 font-medium hover:bg-surface-muted";

/** Side-menu rows: chat and channel (each only when set on the server), and inviting friends. */
export function WhatsAppMenuLinks({ onNavigate }: { onNavigate?: () => void }) {
  const t = useT();
  const config = useAppConfig().data;
  return (
    <ul>
      {config?.whatsAppNumber && (
        <li>
          <a href={whatsAppChatHref(config.whatsAppNumber, t("whatsAppHello", { brand }))} target="_blank" rel="noreferrer" onClick={onNavigate} className={rowClass}>
            <Icon name="chat" className="text-fg-muted" />
            <span className="flex-1">{t("whatsAppChat")}</span>
            <Icon name="external" size={16} className="text-fg-muted" />
          </a>
        </li>
      )}
      {config?.whatsAppChannelUrl && (
        <li>
          <a href={config.whatsAppChannelUrl} target="_blank" rel="noreferrer" onClick={onNavigate} className={rowClass}>
            <Icon name="bell" className="text-fg-muted" />
            <span className="flex-1">{t("whatsAppChannel")}</span>
            <Icon name="external" size={16} className="text-fg-muted" />
          </a>
        </li>
      )}
      <li>
        <a href={whatsAppShareHref(t("shareAppMessage", { brand, url: siteUrl(config?.websiteUrl) }))} target="_blank" rel="noreferrer" onClick={onNavigate} className={rowClass}>
          <Icon name="share" className="text-fg-muted" />
          <span className="flex-1">{t("inviteFriends")}</span>
          <Icon name="external" size={16} className="text-fg-muted" />
        </a>
      </li>
    </ul>
  );
}

/** "Chat with us on WhatsApp" as a card link (support page); nothing when no number is set. */
export function WhatsAppChatCard() {
  const t = useT();
  const number = useAppConfig().data?.whatsAppNumber;
  if (!number) return null;
  return (
    <a
      href={whatsAppChatHref(number, t("whatsAppHello", { brand }))}
      target="_blank"
      rel="noreferrer"
      className="flex items-center gap-3 rounded-2xl border border-border bg-surface p-4 font-medium hover:border-primary"
    >
      <span className="rounded-xl bg-success-soft p-2 text-success">
        <Icon name="chat" />
      </span>
      <span className="flex-1">{t("whatsAppChat")}</span>
      <Icon name="external" size={16} className="text-fg-muted" />
    </a>
  );
}

/** A round "chat on WhatsApp" button in the corner (landing page); nothing when no number is set. */
export function WhatsAppFloatingButton() {
  const t = useT();
  const number = useAppConfig().data?.whatsAppNumber;
  if (!number) return null;
  return (
    <a
      href={whatsAppChatHref(number, t("whatsAppHello", { brand }))}
      target="_blank"
      rel="noreferrer"
      aria-label={t("whatsAppChat")}
      className="fixed bottom-[calc(1rem+env(safe-area-inset-bottom))] right-4 z-30 flex size-14 items-center justify-center rounded-full bg-success text-bg shadow-lg hover:brightness-110"
    >
      <Icon name="chat" size={26} />
    </a>
  );
}

/** "Share on WhatsApp" for a course, with a link to its page. */
export function ShareCourseOnWhatsApp({ courseId, title }: { courseId: string; title: string }) {
  const t = useT();
  const config = useAppConfig().data;
  const url = `${siteUrl(config?.websiteUrl)}/learn/${courseId}`;
  return (
    <a
      href={whatsAppShareHref(t("shareCourseMessage", { course: title, brand, url }))}
      target="_blank"
      rel="noreferrer"
      className="inline-flex items-center gap-1.5 text-sm font-medium text-primary hover:underline"
    >
      <Icon name="share" size={16} />
      {t("shareOnWhatsApp")}
    </a>
  );
}

/** "Ask about admission on WhatsApp" for a course whose classes haven't started; nothing when no number is set. */
export function AskAboutAdmission({ title }: { title: string }) {
  const t = useT();
  const number = useAppConfig().data?.whatsAppNumber;
  if (!number) return null;
  return (
    <a
      href={whatsAppChatHref(number, t("admissionMessage", { course: title }))}
      target="_blank"
      rel="noreferrer"
      className="inline-flex items-center gap-2 rounded-lg bg-success px-4 py-2 text-sm font-medium text-bg hover:brightness-110"
    >
      <Icon name="chat" size={16} />
      {t("askAboutAdmission")}
    </a>
  );
}
