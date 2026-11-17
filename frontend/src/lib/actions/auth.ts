"use server";

import { redirect } from "next/navigation";
import { toLocale } from "@/i18n/messages";
import { clearSession, getRefreshToken, saveSession } from "@/lib/auth/session";
import { apiWithToken, fieldErrorsFrom, safeReturnTo, text, type FormState } from "./shared";

type GuestLine = { bookId: string; quantity: number };

/** The guest cart arrives as JSON in a hidden field, filled from localStorage by the form. */
function parseGuestCart(formData: FormData): GuestLine[] {
  try {
    const parsed: unknown = JSON.parse(text(formData, "guestCart") || "[]");
    return Array.isArray(parsed)
      ? parsed
          .filter(
            (l): l is GuestLine => typeof l?.bookId === "string" && Number.isInteger(l?.quantity),
          )
          .slice(0, 50)
      : [];
  } catch {
    return [];
  }
}

async function mergeGuestCart(accessToken: string, lines: GuestLine[]) {
  if (lines.length === 0) return;
  const api = await apiWithToken(accessToken);
  // Best effort: a failed merge must not block signing in.
  await api.POST("/api/v1/cart/merge", { body: { items: lines } }).catch(() => undefined);
}

export async function login(_prev: FormState, formData: FormData): Promise<FormState> {
  const locale = toLocale(formData.get("locale"));
  const api = await apiWithToken();
  const { data, error, response } = await api.POST("/api/v1/auth/login", {
    body: { email: text(formData, "email"), password: String(formData.get("password") ?? "") },
  });

  if (!data) {
    if (response.status === 429) return { error: "tooManyRequests" };
    if (response.status === 400)
      return { error: "invalidCredentials", fieldErrors: fieldErrorsFrom(error) };
    if (response.status === 401) {
      const detail = (error as { detail?: string } | undefined)?.detail ?? "";
      return { error: detail.startsWith("Too many") ? "locked" : "invalidCredentials" };
    }
    return { error: "genericError" };
  }

  await saveSession(data);
  await mergeGuestCart(data.accessToken, parseGuestCart(formData));
  redirect(safeReturnTo(formData.get("returnTo"), locale));
}

export async function register(_prev: FormState, formData: FormData): Promise<FormState> {
  const locale = toLocale(formData.get("locale"));
  const phone = text(formData, "phoneNumber");
  const api = await apiWithToken();
  const { data, error, response } = await api.POST("/api/v1/auth/register", {
    body: {
      fullName: text(formData, "fullName"),
      email: text(formData, "email"),
      password: String(formData.get("password") ?? ""),
      phoneNumber: phone || null,
    },
  });

  if (!data) {
    if (response.status === 409) return { error: "emailTaken" };
    if (response.status === 429) return { error: "tooManyRequests" };
    if (response.status === 400)
      return { error: "genericError", fieldErrors: fieldErrorsFrom(error) };
    return { error: "genericError" };
  }

  await saveSession(data);
  await mergeGuestCart(data.accessToken, parseGuestCart(formData));
  redirect(safeReturnTo(formData.get("returnTo"), locale));
}

export async function logout(formData: FormData) {
  const locale = toLocale(formData.get("locale"));
  const refreshToken = await getRefreshToken();
  if (refreshToken) {
    const api = await apiWithToken();
    await api.POST("/api/v1/auth/logout", { body: { refreshToken } }).catch(() => undefined);
  }
  await clearSession();
  redirect(`/${locale}`);
}
