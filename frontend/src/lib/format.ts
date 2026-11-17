import type { Locale } from "@/i18n/config";

// Group thousands with a narrow no-break space, as written in Algeria: 2 500.
const grouping = new Intl.NumberFormat("fr-FR", { maximumFractionDigits: 0 });

const currencyLabel: Record<Locale, string> = { ar: "د.ج", fr: "DA", en: "DZD" };

/** Formats whole dinars: "2 500 د.ج" (ar), "2 500 DA" (fr), "2 500 DZD" (en). */
export function formatDzd(amount: number, locale: Locale): string {
  return `${grouping.format(amount)} ${currencyLabel[locale]}`;
}

/** Year of an ISO date string ("1993-01-01" -> "1993"); our seed data only knows the year. */
export function formatYear(isoDate: string | null | undefined): string | null {
  return isoDate ? isoDate.slice(0, 4) : null;
}
