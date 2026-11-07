"use client";

import Link from "next/link";
import { useSearchParams } from "next/navigation";
import { useEffect, useState } from "react";
import { useForm } from "react-hook-form";
import { rawFetch } from "@/lib/api/client";
import { useT } from "@/lib/i18n";
import { Button } from "@/components/ui/button";
import { Field, Input } from "@/components/ui/field";
import { AuthLayout } from "./auth-layout";
import { StudentError } from "./errors";
import { safeNext } from "./profile";
import { TakeOverDialog, otherDevice, type OtherDevice } from "./take-over";

type SessionStart = { userId: string; roles: string[]; onboardingComplete: boolean };

const post = (path: string, body: unknown) =>
  rawFetch<SessionStart>(path, { method: "POST", headers: { "content-type": "application/json" }, body: JSON.stringify(body) });

/** After any successful sign-in: onboarding first if unfinished. A full load resets cached data from any previous session. */
function enter(session: SessionStart, next: string | null) {
  const target = safeNext(next);
  const url = session.onboardingComplete ? target : `/onboarding${target !== "/home" ? `?next=${encodeURIComponent(target)}` : ""}`;
  // A full load (not router.push) clears every cached query after a session change.
  window.location.assign(url);
}

const EMAIL = /^[^\s@]+@[^\s@]+\.[^\s@]+$/;
/** Accepts 98765 43210, +91 98765 43210 or 098765 43210; returns the 10 digits or null. */
export function parseIndianMobile(input: string): string | null {
  let d = input.replace(/\D/g, "");
  if (d.length === 12 && d.startsWith("91")) d = d.slice(2);
  else if (d.length === 11 && d.startsWith("0")) d = d.slice(1);
  return /^[6-9]\d{9}$/.test(d) ? d : null;
}

function Terms({ checked, onChange, error }: { checked: boolean; onChange: (v: boolean) => void; error?: string }) {
  const t = useT();
  return (
    <div>
      <label className="flex items-start gap-2 text-sm">
        <input type="checkbox" className="mt-0.5 size-4 accent-[var(--primary)]" checked={checked} onChange={(e) => onChange(e.target.checked)} aria-invalid={!!error} />
        <span>{t("acceptTerms")}</span>
      </label>
      {error && (
        <p className="mt-1 text-xs text-danger" role="alert">
          {error}
        </p>
      )}
    </div>
  );
}

function EmailSignIn({ next }: { next: string | null }) {
  const t = useT();
  const [error, setError] = useState<unknown>(null);
  const [other, setOther] = useState<{ device: OtherDevice; values: { email: string; password: string } } | null>(null);
  const [takingOver, setTakingOver] = useState(false);
  const { register, handleSubmit, formState } = useForm<{ email: string; password: string }>();
  const signIn = async (v: { email: string; password: string }, takeOver: boolean) => {
    setError(null);
    try {
      enter(await post("/api/session/login", { ...v, takeOver }), next);
    } catch (e) {
      const device = otherDevice(e);
      if (device && !takeOver) setOther({ device, values: v });
      else setError(e);
    }
  };
  const submit = handleSubmit((v) => signIn(v, false));
  const continueHere = async () => {
    setTakingOver(true);
    await signIn(other!.values, true);
    setTakingOver(false);
    setOther(null);
  };
  return (
    <form onSubmit={submit} className="space-y-4" noValidate>
      <TakeOverDialog device={other?.device ?? null} busy={takingOver} onContinue={continueHere} onCancel={() => setOther(null)} />
      <StudentError error={error} />
      <Field label={t("email")} error={formState.errors.email?.message}>
        <Input
          type="email"
          autoComplete="username"
          aria-invalid={!!formState.errors.email}
          {...register("email", { required: t("fieldRequired"), pattern: { value: EMAIL, message: t("invalidEmail") } })}
        />
      </Field>
      <Field label={t("password")} error={formState.errors.password?.message}>
        <Input type="password" autoComplete="current-password" aria-invalid={!!formState.errors.password} {...register("password", { required: t("fieldRequired") })} />
      </Field>
      <Button type="submit" className="w-full" loading={formState.isSubmitting}>
        {t("signIn")}
      </Button>
    </form>
  );
}

const RESEND_SECONDS = 30;

function MobileSignIn({ next }: { next: string | null }) {
  const t = useT();
  const [phone, setPhone] = useState<string | null>(null);
  const [error, setError] = useState<unknown>(null);
  const [wait, setWait] = useState(0);
  const phoneForm = useForm<{ phone: string }>();
  const codeForm = useForm<{ code: string; fullName: string }>();
  const [terms, setTerms] = useState(false);

  useEffect(() => {
    if (wait <= 0) return;
    const id = setTimeout(() => setWait((w) => w - 1), 1000);
    return () => clearTimeout(id);
  }, [wait]);

  const send = async (digits: string) => {
    setError(null);
    try {
      await rawFetch("/api/session/otp/request", { method: "POST", headers: { "content-type": "application/json" }, body: JSON.stringify({ phone: digits }) });
      setPhone(digits);
      setWait(RESEND_SECONDS);
    } catch (e) {
      setError(e);
    }
  };

  const requestCode = phoneForm.handleSubmit(async (v) => send(parseIndianMobile(v.phone)!));
  const [other, setOther] = useState<{ device: OtherDevice; values: { code: string; fullName: string } } | null>(null);
  const [takingOver, setTakingOver] = useState(false);
  const verifyWith = async (v: { code: string; fullName: string }, takeOver: boolean) => {
    setError(null);
    try {
      enter(await post("/api/session/otp/verify", { phone, code: v.code, fullName: v.fullName.trim() || null, acceptTerms: terms, takeOver }), next);
    } catch (e) {
      const device = otherDevice(e);
      if (device && !takeOver) setOther({ device, values: v });
      else setError(e);
    }
  };
  const verify = codeForm.handleSubmit((v) => verifyWith(v, false));
  const continueHere = async () => {
    setTakingOver(true);
    await verifyWith(other!.values, true);
    setTakingOver(false);
    setOther(null);
  };

  if (!phone) {
    return (
      <form onSubmit={requestCode} className="space-y-4" noValidate>
        <StudentError error={error} />
        <Field label={t("mobileNumber")} error={phoneForm.formState.errors.phone?.message}>
          <div className="flex">
            <span className="inline-flex h-10 items-center rounded-l-lg border border-r-0 border-border bg-surface-muted px-3 text-sm text-fg-muted">+91</span>
            <Input
              type="tel"
              inputMode="numeric"
              autoComplete="tel-national"
              className="rounded-l-none"
              aria-invalid={!!phoneForm.formState.errors.phone}
              {...phoneForm.register("phone", { required: t("fieldRequired"), validate: (v) => (parseIndianMobile(v) ? true : t("invalidPhone")) })}
            />
          </div>
        </Field>
        <Button type="submit" className="w-full" loading={phoneForm.formState.isSubmitting}>
          {t("sendOtp")}
        </Button>
      </form>
    );
  }

  return (
    <form onSubmit={verify} className="space-y-4" noValidate>
      <TakeOverDialog device={other?.device ?? null} busy={takingOver} onContinue={continueHere} onCancel={() => setOther(null)} />
      <p className="text-sm text-fg-muted">
        {t("otpSentTo", { phone: `+91 ${phone.slice(0, 5)} ${phone.slice(5)}` })}{" "}
        <button
          type="button"
          className="font-medium text-primary hover:underline"
          onClick={() => {
            setPhone(null);
            setError(null);
            codeForm.reset();
          }}
        >
          {t("changeNumber")}
        </button>
      </p>
      <StudentError error={error} />
      <Field label={t("enterOtp")} error={codeForm.formState.errors.code?.message}>
        <Input
          inputMode="numeric"
          autoComplete="one-time-code"
          maxLength={6}
          className="text-center text-lg tracking-[0.5em]"
          aria-invalid={!!codeForm.formState.errors.code}
          {...codeForm.register("code", { required: t("fieldRequired"), pattern: { value: /^\d{6}$/, message: t("enterOtp") } })}
        />
      </Field>
      <Field label={t("yourNameNewAccounts")}>
        <Input autoComplete="name" {...codeForm.register("fullName")} />
      </Field>
      <Terms checked={terms} onChange={setTerms} />
      <Button type="submit" className="w-full" loading={codeForm.formState.isSubmitting}>
        {t("verify")}
      </Button>
      <p className="text-center text-sm">
        {wait > 0 ? (
          <span className="text-fg-muted">{t("resendIn", { seconds: wait })}</span>
        ) : (
          <button type="button" className="font-medium text-primary hover:underline" onClick={() => send(phone)}>
            {t("resendOtp")}
          </button>
        )}
      </p>
    </form>
  );
}

export function StudentLogin() {
  const t = useT();
  const params = useSearchParams();
  const next = params.get("next");
  const [method, setMethod] = useState<"mobile" | "email">("mobile");
  const registerHref = next ? `/register?next=${encodeURIComponent(next)}` : "/register";
  return (
    <AuthLayout
      title={t("welcomeBack")}
      subtitle={t("signInSubtitle")}
      footer={
        <Link href={registerHref} className="font-medium text-primary hover:underline">
          {t("newHereCreateAccount")}
        </Link>
      }
    >
      {params.get("replaced") ? (
        <p role="status" className="mb-4 rounded-lg bg-warning-soft px-3 py-2 text-sm text-warning">
          {t("sessionReplaced")}
        </p>
      ) : (
        params.get("expired") && <p className="mb-4 rounded-lg bg-warning-soft px-3 py-2 text-sm text-warning">{t("sessionExpired")}</p>
      )}
      {params.get("deleted") && (
        <p role="status" className="mb-4 rounded-lg bg-primary-soft px-3 py-2 text-sm">
          {t("deleteRequested")}
        </p>
      )}
      <div role="tablist" aria-label={t("signIn")} className="mb-5 grid grid-cols-2 gap-1 rounded-lg bg-surface-muted p-1 text-sm">
        {(["mobile", "email"] as const).map((m) => (
          <button
            key={m}
            type="button"
            role="tab"
            aria-selected={method === m}
            onClick={() => setMethod(m)}
            className={`rounded-md px-3 py-2 font-medium ${method === m ? "bg-surface text-fg shadow-sm" : "text-fg-muted hover:text-fg"}`}
          >
            {m === "mobile" ? t("mobileNumber") : t("email")}
          </button>
        ))}
      </div>
      <div role="tabpanel">{method === "mobile" ? <MobileSignIn next={next} /> : <EmailSignIn next={next} />}</div>
    </AuthLayout>
  );
}

export function StudentRegister() {
  const t = useT();
  const params = useSearchParams();
  const next = params.get("next");
  const [error, setError] = useState<unknown>(null);
  const { register, handleSubmit, formState } = useForm<{ fullName: string; email: string; password: string; terms: boolean }>();
  const submit = handleSubmit(async (v) => {
    setError(null);
    try {
      enter(await post("/api/session/register", { fullName: v.fullName.trim(), email: v.email.trim(), password: v.password, acceptTerms: v.terms }), next);
    } catch (e) {
      setError(e);
    }
  });
  return (
    <AuthLayout
      title={t("createAccount")}
      subtitle={t("createAccountSubtitle")}
      footer={
        <Link href={next ? `/login?next=${encodeURIComponent(next)}` : "/login"} className="font-medium text-primary hover:underline">
          {t("haveAccountSignIn")}
        </Link>
      }
    >
      <form onSubmit={submit} className="space-y-4" noValidate>
        <StudentError error={error} />
        <Field label={t("fullName")} error={formState.errors.fullName?.message}>
          <Input autoComplete="name" aria-invalid={!!formState.errors.fullName} {...register("fullName", { required: t("fieldRequired"), validate: (v) => !!v.trim() || t("fieldRequired") })} />
        </Field>
        <Field label={t("email")} error={formState.errors.email?.message}>
          <Input
            type="email"
            autoComplete="email"
            aria-invalid={!!formState.errors.email}
            {...register("email", { required: t("fieldRequired"), pattern: { value: EMAIL, message: t("invalidEmail") } })}
          />
        </Field>
        <Field label={t("password")} error={formState.errors.password?.message}>
          <Input
            type="password"
            autoComplete="new-password"
            aria-invalid={!!formState.errors.password}
            {...register("password", { required: t("fieldRequired"), minLength: { value: 8, message: t("passwordTooShort") } })}
          />
        </Field>
        <div>
          <label className="flex items-start gap-2 text-sm">
            <input type="checkbox" className="mt-0.5 size-4 accent-[var(--primary)]" aria-invalid={!!formState.errors.terms} {...register("terms", { validate: (v) => v || t("mustAcceptTerms") })} />
            <span>{t("acceptTerms")}</span>
          </label>
          {formState.errors.terms && (
            <p className="mt-1 text-xs text-danger" role="alert">
              {formState.errors.terms.message}
            </p>
          )}
        </div>
        <Button type="submit" className="w-full" loading={formState.isSubmitting}>
          {t("createAccount")}
        </Button>
      </form>
    </AuthLayout>
  );
}
