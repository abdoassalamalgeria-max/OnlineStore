"use server";

import { redirect } from "next/navigation";
import { toLocale } from "@/i18n/messages";
import { getAccessToken, saveSession } from "@/lib/auth/session";
import { apiWithToken, fieldErrorsFrom, text, type FormState } from "./shared";

export type AccountFormState = (FormState & { ok?: boolean }) | null;

async function requireToken(formData: FormData) {
  const token = await getAccessToken();
  if (!token) {
    const locale = toLocale(formData.get("locale"));
    redirect(`/${locale}/login?returnTo=${encodeURIComponent(`/${locale}/account`)}`);
  }
  return token;
}

export async function updateProfile(
  _prev: AccountFormState,
  formData: FormData,
): Promise<AccountFormState> {
  const api = await apiWithToken(await requireToken(formData));
  const phone = text(formData, "phoneNumber");
  const { data, error, response } = await api.PUT("/api/v1/account/profile", {
    body: { fullName: text(formData, "fullName"), phoneNumber: phone || null },
  });
  if (data) return { ok: true };
  return response.status === 400
    ? { fieldErrors: fieldErrorsFrom(error) }
    : { error: "genericError" };
}

export async function changePassword(
  _prev: AccountFormState,
  formData: FormData,
): Promise<AccountFormState> {
  const api = await apiWithToken(await requireToken(formData));
  const { data, error, response } = await api.POST("/api/v1/account/password", {
    body: {
      currentPassword: String(formData.get("currentPassword") ?? ""),
      newPassword: String(formData.get("newPassword") ?? ""),
    },
  });
  if (data) {
    // Other sessions were revoked; this one continues with the fresh tokens.
    await saveSession(data);
    return { ok: true };
  }
  return response.status === 400
    ? { fieldErrors: fieldErrorsFrom(error) }
    : { error: "genericError" };
}
