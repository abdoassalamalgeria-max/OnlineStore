import type { Dictionary } from "@/i18n/messages";

/** The status texts every AdminForm needs, picked from the dictionary. */
export const formMessages = (t: Dictionary) => ({
  saved: t.admin.saved,
  failed: t.admin.actionFailed,
  blocked: t.admin.deleteBlocked,
  fixErrors: t.checkout.fixErrors,
});

export const inputClass = "w-full rounded-md border border-border bg-background px-3 py-2 text-sm";
export const labelClass = "mb-1 block text-sm font-medium";
