"use client";

import { useActionState } from "react";
import type { Locale } from "@/i18n/config";
import type { Dictionary } from "@/i18n/messages";
import { changePassword, updateProfile } from "@/lib/actions/account";
import { FieldError, inputClass, SubmitButton } from "./form-bits";

type Props = { locale: Locale; t: Dictionary; loading: string };

function Status({
  ok,
  error,
  okText,
  t,
}: {
  ok?: boolean;
  error?: string;
  okText: string;
  t: Dictionary;
}) {
  if (ok)
    return (
      <p role="status" className="text-brand text-sm">
        {okText}
      </p>
    );
  if (error)
    return (
      <p role="alert" className="text-accent text-sm">
        {t.auth.genericError}
      </p>
    );
  return null;
}

export function ProfileForm({
  locale,
  t,
  loading,
  defaults,
}: Props & { defaults: { fullName: string; phoneNumber: string | null; email: string } }) {
  const [state, action] = useActionState(updateProfile, null);
  const errors = state?.fieldErrors ?? {};
  const a = t.account;

  return (
    <form action={action} className="flex flex-col gap-4" noValidate>
      <input type="hidden" name="locale" value={locale} />
      <div>
        <span className="mb-1 block text-sm font-medium">{a.email}</span>
        <p dir="ltr" className="text-muted text-start">
          {defaults.email}
        </p>
      </div>
      <div>
        <label htmlFor="fullName" className="mb-1 block text-sm font-medium">
          {a.fullName}
        </label>
        <input
          id="fullName"
          name="fullName"
          autoComplete="name"
          defaultValue={defaults.fullName}
          aria-invalid={errors.fullName ? true : undefined}
          aria-describedby="fullName-error"
          className={inputClass}
        />
        <FieldError id="fullName-error" messages={errors.fullName} />
      </div>
      <div>
        <label htmlFor="phoneNumber" className="mb-1 block text-sm font-medium">
          {a.phone}
        </label>
        <input
          id="phoneNumber"
          name="phoneNumber"
          type="tel"
          inputMode="tel"
          dir="ltr"
          autoComplete="tel"
          defaultValue={defaults.phoneNumber ?? ""}
          aria-invalid={errors.phoneNumber ? true : undefined}
          aria-describedby="phoneNumber-error"
          className={inputClass}
        />
        <FieldError id="phoneNumber-error" messages={errors.phoneNumber} />
      </div>
      <Status ok={state?.ok} error={state?.error} okText={`${a.saved} ${a.nameChangeNote}`} t={t} />
      <div className="sm:w-40">
        <SubmitButton pendingLabel={loading}>{a.save}</SubmitButton>
      </div>
    </form>
  );
}

export function PasswordForm({ locale, t, loading }: Props) {
  const [state, action] = useActionState(changePassword, null);
  const errors = state?.fieldErrors ?? {};
  const a = t.account;

  return (
    <form action={action} className="flex flex-col gap-4" noValidate>
      <input type="hidden" name="locale" value={locale} />
      <div>
        <label htmlFor="currentPassword" className="mb-1 block text-sm font-medium">
          {a.currentPassword}
        </label>
        <input
          id="currentPassword"
          name="currentPassword"
          type="password"
          autoComplete="current-password"
          dir="ltr"
          aria-invalid={errors.currentPassword ? true : undefined}
          aria-describedby="currentPassword-error"
          className={inputClass}
        />
        <FieldError id="currentPassword-error" messages={errors.currentPassword} />
      </div>
      <div>
        <label htmlFor="newPassword" className="mb-1 block text-sm font-medium">
          {a.newPassword}
        </label>
        <input
          id="newPassword"
          name="newPassword"
          type="password"
          autoComplete="new-password"
          dir="ltr"
          aria-invalid={errors.newPassword ? true : undefined}
          aria-describedby="newPassword-hint newPassword-error"
          className={inputClass}
        />
        <p id="newPassword-hint" className="text-muted mt-1 text-xs">
          {t.auth.passwordHint}
        </p>
        <FieldError id="newPassword-error" messages={errors.newPassword} />
      </div>
      <Status ok={state?.ok} error={state?.error} okText={a.passwordChanged} t={t} />
      <div className="sm:w-56">
        <SubmitButton pendingLabel={loading}>{a.changePassword}</SubmitButton>
      </div>
    </form>
  );
}
