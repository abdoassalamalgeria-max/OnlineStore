import { redirect } from "next/navigation";
import type { Locale } from "@/i18n/config";
import { getAccessToken } from "./session";

/** Sends guests to the login page, coming back to <paramref name="returnTo"/> afterwards. */
export async function requireSignedIn(locale: Locale, returnTo: string) {
  if (!(await getAccessToken())) {
    redirect(`/${locale}/login?returnTo=${encodeURIComponent(returnTo)}`);
  }
}
