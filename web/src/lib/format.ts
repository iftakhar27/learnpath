const inr = new Intl.NumberFormat("en-IN", { style: "currency", currency: "INR", minimumFractionDigits: 0, maximumFractionDigits: 2 });
const dateTime = new Intl.DateTimeFormat("en-IN", { timeZone: "Asia/Kolkata", day: "2-digit", month: "short", year: "numeric", hour: "2-digit", minute: "2-digit" });
const dateOnly = new Intl.DateTimeFormat("en-IN", { timeZone: "Asia/Kolkata", day: "2-digit", month: "short", year: "numeric" });
const integer = new Intl.NumberFormat("en-IN");

/** Amounts are stored in paise (₹1 = 100 paise). */
export const formatPaise = (paise: number) => inr.format(paise / 100);
export const rupeesToPaise = (rupees: number) => Math.round(rupees * 100);
export const paiseToRupees = (paise: number) => paise / 100;

export const formatNumber = (n: number) => integer.format(n);
export const formatDateTime = (iso: string | null | undefined) => (iso ? dateTime.format(new Date(iso)) : "—");
export const formatDate = (iso: string | null | undefined) => (iso ? dateOnly.format(new Date(iso)) : "—");

/** `datetime-local` inputs work in local wall-clock time; convert to/from ISO for the API. */
export function toLocalInput(iso: string | null | undefined): string {
  if (!iso) return "";
  const d = new Date(iso);
  const pad = (n: number) => String(n).padStart(2, "0");
  return `${d.getFullYear()}-${pad(d.getMonth() + 1)}-${pad(d.getDate())}T${pad(d.getHours())}:${pad(d.getMinutes())}`;
}
export const fromLocalInput = (value: string) => (value ? new Date(value).toISOString() : null);

export const humanize = (s: string) => s.replace(/([a-z])([A-Z])/g, "$1 $2");

export function truncate(s: string, max = 120) {
  return s.length <= max ? s : s.slice(0, max - 1) + "…";
}

/** Empty strings from form inputs become null for optional API fields. */
export const blankToNull = (s: string | null | undefined) => (s && s.trim() ? s.trim() : null);
