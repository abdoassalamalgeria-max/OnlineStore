import createClient from "openapi-fetch";
import { headers } from "next/headers";
import type { Locale } from "@/i18n/config";
import type { paths } from "@/lib/api/schema";

/** Result of a form Server Action, consumed by useActionState in the form component. */
export type FormState = {
  /** Key into the form's translations (e.g. "invalidCredentials"). */
  error?: string;
  /** Field name → API validation messages. */
  fieldErrors?: Record<string, string[]>;
} | null;

/** API client for an explicit token (e.g. right after login, before the cookie round-trips). */
export async function apiWithToken(token?: string) {
  const forwardedFor = (await headers()).get("x-forwarded-for");
  const requestHeaders: Record<string, string> = {};
  if (token) requestHeaders.Authorization = `Bearer ${token}`;
  if (forwardedFor) requestHeaders["X-Forwarded-For"] = forwardedFor;
  return createClient<paths>({
    baseUrl: process.env.API_URL ?? "http://localhost:5080",
    headers: requestHeaders,
    cache: "no-store",
  });
}

/** Turns ValidationProblemDetails.errors into { field: messages } with the last path segment as key. */
export function fieldErrorsFrom(problem: unknown): Record<string, string[]> | undefined {
  const errors = (problem as { errors?: Record<string, string[]> } | undefined)?.errors;
  if (!errors) return undefined;
  return Object.fromEntries(
    Object.entries(errors).map(([key, messages]) => {
      const last = key.split(".").pop() ?? key;
      return [last.charAt(0).toLowerCase() + last.slice(1), messages];
    }),
  );
}

/** Only same-site paths under the current locale are allowed as post-login destinations. */
export function safeReturnTo(value: FormDataEntryValue | null, locale: Locale): string {
  const path = typeof value === "string" ? value : "";
  return path.startsWith(`/${locale}`) && !path.startsWith("//") && !path.includes("\\")
    ? path
    : `/${locale}`;
}

export function text(formData: FormData, name: string): string {
  const value = formData.get(name);
  return typeof value === "string" ? value.trim() : "";
}
