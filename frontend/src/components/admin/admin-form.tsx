"use client";

import { useActionState, type ReactNode } from "react";
import type { AdminState } from "@/lib/actions/admin";
import { SubmitButton } from "../form-bits";

type Props = {
  action: (prev: AdminState, formData: FormData) => Promise<AdminState>;
  children: ReactNode;
  submitLabel: string;
  pendingLabel: string;
  messages: { saved: string; failed: string; blocked: string; fixErrors: string };
  /** Asks for confirmation before submitting (destructive actions). */
  confirm?: string;
  className?: string;
  /** Visual style of the submit button. */
  variant?: "primary" | "danger";
};

/**
 * Generic admin form: runs a Server Action, then shows success or the API's validation messages.
 * Fields are passed as children, so they can be rendered on the server.
 */
export function AdminForm({
  action,
  children,
  submitLabel,
  pendingLabel,
  messages,
  confirm,
  className,
  variant = "primary",
}: Props) {
  const [state, formAction] = useActionState(action, null);
  const fieldErrors = Object.entries(state?.fieldErrors ?? {}).filter(([, list]) =>
    list.some((m) => m && m !== "—"),
  );

  return (
    <form
      action={formAction}
      className={className ?? "flex flex-col gap-4"}
      onSubmit={(event) => {
        if (confirm && !window.confirm(confirm)) event.preventDefault();
      }}
    >
      {children}

      {state?.ok && (
        <p role="status" className="text-brand text-sm">
          {messages.saved}
        </p>
      )}
      {state?.error && (
        <div
          role="alert"
          className="border-accent/40 bg-accent/10 text-accent rounded-md border p-3 text-sm"
        >
          <p>
            {state.error === "deleteBlocked"
              ? messages.blocked
              : state.error === "fixErrors"
                ? messages.fixErrors
                : messages.failed}
          </p>
          {fieldErrors.length > 0 && (
            <ul className="mt-1 list-disc ps-5">
              {fieldErrors.map(([field, list]) => (
                <li key={field}>
                  <span dir="ltr" className="font-mono">
                    {field}
                  </span>
                  : {list.join(" ")}
                </li>
              ))}
            </ul>
          )}
        </div>
      )}

      <div className={variant === "danger" ? "[&_button]:bg-accent w-fit" : "w-fit min-w-40"}>
        <SubmitButton pendingLabel={pendingLabel}>{submitLabel}</SubmitButton>
      </div>
    </form>
  );
}
