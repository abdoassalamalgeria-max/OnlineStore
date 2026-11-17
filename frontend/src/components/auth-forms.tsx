"use client";

import Link from "next/link";
import { useActionState } from "react";
import type { Locale } from "@/i18n/config";
import type { Dictionary } from "@/i18n/messages";
import { login, register } from "@/lib/actions/auth";
import type { FormState } from "@/lib/actions/shared";
import { FieldError, GuestCartField, inputClass, SubmitButton } from "./form-bits";

type Props = { locale: Locale; returnTo: string; t: Dictionary["auth"]; loading: string };

function FormError({ state, t }: { state: FormState; t: Dictionary["auth"] }) {
  if (!state?.error) return null;
  const message = t[state.error as keyof Dictionary["auth"]] ?? t.genericError;
  return (
    <p
      role="alert"
      className="border-accent/40 bg-accent/10 text-accent rounded-md border p-3 text-sm"
    >
      {message}
    </p>
  );
}

export function LoginForm({ locale, returnTo, t, loading }: Props) {
  const [state, action] = useActionState(login, null);

  return (
    <form action={action} className="flex flex-col gap-4" noValidate>
      <input type="hidden" name="locale" value={locale} />
      <input type="hidden" name="returnTo" value={returnTo} />
      <GuestCartField />
      <FormError state={state} t={t} />

      <div>
        <label htmlFor="email" className="mb-1 block text-sm font-medium">
          {t.email}
        </label>
        <input
          id="email"
          name="email"
          type="email"
          autoComplete="email"
          required
          dir="ltr"
          className={inputClass}
        />
      </div>
      <div>
        <label htmlFor="password" className="mb-1 block text-sm font-medium">
          {t.password}
        </label>
        <input
          id="password"
          name="password"
          type="password"
          autoComplete="current-password"
          required
          dir="ltr"
          className={inputClass}
        />
      </div>

      <SubmitButton pendingLabel={loading}>{t.submitLogin}</SubmitButton>
      <p className="text-muted text-center text-sm">
        {t.noAccount}{" "}
        <Link
          href={`/${locale}/register?returnTo=${encodeURIComponent(returnTo)}`}
          className="text-brand underline"
        >
          {t.registerTitle}
        </Link>
      </p>
    </form>
  );
}

export function RegisterForm({ locale, returnTo, t, loading }: Props) {
  const [state, action] = useActionState(register, null);
  const errors = state?.fieldErrors ?? {};
  const invalid = (name: string) => (errors[name] ? true : undefined);

  return (
    <form action={action} className="flex flex-col gap-4" noValidate>
      <input type="hidden" name="locale" value={locale} />
      <input type="hidden" name="returnTo" value={returnTo} />
      <GuestCartField />
      <FormError state={state} t={t} />

      <div>
        <label htmlFor="fullName" className="mb-1 block text-sm font-medium">
          {t.fullName}
        </label>
        <input
          id="fullName"
          name="fullName"
          autoComplete="name"
          required
          aria-invalid={invalid("fullName")}
          aria-describedby="fullName-error"
          className={inputClass}
        />
        <FieldError id="fullName-error" messages={errors.fullName} />
      </div>
      <div>
        <label htmlFor="email" className="mb-1 block text-sm font-medium">
          {t.email}
        </label>
        <input
          id="email"
          name="email"
          type="email"
          autoComplete="email"
          required
          dir="ltr"
          aria-invalid={invalid("email")}
          aria-describedby="email-error"
          className={inputClass}
        />
        <FieldError id="email-error" messages={errors.email} />
      </div>
      <div>
        <label htmlFor="phoneNumber" className="mb-1 block text-sm font-medium">
          {t.phone}
        </label>
        <input
          id="phoneNumber"
          name="phoneNumber"
          type="tel"
          inputMode="tel"
          autoComplete="tel"
          dir="ltr"
          placeholder="0550123456"
          aria-invalid={invalid("phoneNumber")}
          aria-describedby="phoneNumber-error"
          className={inputClass}
        />
        <FieldError id="phoneNumber-error" messages={errors.phoneNumber} />
      </div>
      <div>
        <label htmlFor="password" className="mb-1 block text-sm font-medium">
          {t.password}
        </label>
        <input
          id="password"
          name="password"
          type="password"
          autoComplete="new-password"
          required
          dir="ltr"
          aria-invalid={invalid("password")}
          aria-describedby="password-hint password-error"
          className={inputClass}
        />
        <p id="password-hint" className="text-muted mt-1 text-xs">
          {t.passwordHint}
        </p>
        <FieldError id="password-error" messages={errors.password} />
      </div>

      <SubmitButton pendingLabel={loading}>{t.submitRegister}</SubmitButton>
      <p className="text-muted text-center text-sm">
        {t.haveAccount}{" "}
        <Link
          href={`/${locale}/login?returnTo=${encodeURIComponent(returnTo)}`}
          className="text-brand underline"
        >
          {t.loginTitle}
        </Link>
      </p>
    </form>
  );
}
