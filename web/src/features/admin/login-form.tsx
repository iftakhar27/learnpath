"use client";

import { useSearchParams } from "next/navigation";
import { useState } from "react";
import { useForm } from "react-hook-form";
import { rawFetch } from "@/lib/api/client";
import { Button } from "@/components/ui/button";
import { InlineError } from "@/components/ui/feedback";
import { Field, Input } from "@/components/ui/field";

type Form = { email: string; password: string };

/** Only allow redirects back into the admin area, never to another site. */
function safeNext(next: string | null) {
  return next && next.startsWith("/admin") && !next.startsWith("//") ? next : "/admin";
}

export function LoginForm() {
  const params = useSearchParams();
  const [error, setError] = useState<unknown>(null);
  const { register, handleSubmit, formState } = useForm<Form>();

  const onSubmit = handleSubmit(async (values) => {
    setError(null);
    try {
      await rawFetch("/api/session/login", {
        method: "POST",
        headers: { "content-type": "application/json" },
        body: JSON.stringify({ ...values, staffOnly: true }),
      });
      // eslint-disable-next-line @next/next/no-location-assign-relative-destination -- full reload clears every cached query after a session change
      window.location.assign(safeNext(params.get("next")));
    } catch (e) {
      setError(e);
    }
  });

  return (
    <form onSubmit={onSubmit} className="mt-5 space-y-4" noValidate>
      {params.get("expired") && !error && <p className="rounded-lg bg-warning-soft px-3 py-2 text-sm text-warning">Your session ended. Please sign in again.</p>}
      <InlineError error={error} />
      <Field label="Email" htmlFor="email" error={formState.errors.email?.message}>
        <Input id="email" type="email" autoComplete="username" autoFocus {...register("email", { required: "Enter your email" })} aria-invalid={!!formState.errors.email} />
      </Field>
      <Field label="Password" htmlFor="password" error={formState.errors.password?.message}>
        <Input id="password" type="password" autoComplete="current-password" {...register("password", { required: "Enter your password" })} aria-invalid={!!formState.errors.password} />
      </Field>
      <Button type="submit" className="w-full" loading={formState.isSubmitting}>
        Sign in
      </Button>
    </form>
  );
}
