import { isLocale, defaultLocale, type Locale } from "./config";
import type ar from "./messages/ar.json";

/** The Arabic file is the source of truth for keys; fr/en must have the same shape. */
export type Dictionary = typeof ar;

const dictionaries: Record<Locale, () => Promise<Dictionary>> = {
  ar: () => import("./messages/ar.json").then((m) => m.default),
  fr: () => import("./messages/fr.json").then((m) => m.default),
  en: () => import("./messages/en.json").then((m) => m.default),
};

/** Loads a dictionary for an explicit locale (usable in Server Actions, which have no root params). */
export function loadDictionary(locale: Locale): Promise<Dictionary> {
  return dictionaries[locale]();
}

/** Normalises untrusted input (e.g. a hidden form field) to a supported locale. */
export function toLocale(value: unknown): Locale {
  return typeof value === "string" && isLocale(value) ? value : defaultLocale;
}

/** Replaces {name} placeholders: format("{count} books", { count: 3 }). Safe in Client Components. */
export function format(template: string, values: Record<string, string | number>): string {
  return template.replace(/\{(\w+)\}/g, (match, key: string) =>
    key in values ? String(values[key]) : match,
  );
}
