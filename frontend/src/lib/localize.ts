import type { Locale } from "@/i18n/config";

type Named = { nameAr: string; nameFr: string; nameEn: string };

/** Picks the category name for the UI language. */
export function localizedName(item: Named, locale: Locale): string {
  return locale === "ar" ? item.nameAr : locale === "fr" ? item.nameFr : item.nameEn;
}
