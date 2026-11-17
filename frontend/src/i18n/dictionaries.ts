import { notFound } from "next/navigation";
import { locale as rootLocale } from "next/root-params";
import { isLocale, type Locale } from "./config";
import { format, loadDictionary, type Dictionary } from "./messages";

export { format, type Dictionary };

/** The current request's locale, read from the root [locale] segment. Server Components only. */
export async function getLocale(): Promise<Locale> {
  const value = await rootLocale();
  if (!isLocale(value)) {
    notFound();
  }
  return value;
}

/** Translations for the current locale. Server Components only (pass slices to Client Components as props). */
export async function getDictionary(): Promise<Dictionary> {
  return loadDictionary(await getLocale());
}
