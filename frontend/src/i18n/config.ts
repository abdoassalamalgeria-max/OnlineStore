// Supported UI languages. Arabic is the default and renders right-to-left.
export const locales = ["ar", "fr", "en"] as const;
export type Locale = (typeof locales)[number];
export const defaultLocale: Locale = "ar";

/** Cookie remembering the visitor's explicit language choice (set by the language switcher). */
export const localeCookie = "NEXT_LOCALE";

export const isLocale = (value: string | undefined): value is Locale =>
  value !== undefined && (locales as readonly string[]).includes(value);

export const directionOf = (locale: Locale): "rtl" | "ltr" => (locale === "ar" ? "rtl" : "ltr");

/** Native names, shown in the language switcher. */
export const localeNames: Record<Locale, string> = {
  ar: "العربية",
  fr: "Français",
  en: "English",
};

/** BCP 47 tags for Intl formatting (Algeria-specific where it matters). */
export const intlLocale: Record<Locale, string> = {
  ar: "ar-DZ",
  fr: "fr-DZ",
  en: "en-GB",
};
