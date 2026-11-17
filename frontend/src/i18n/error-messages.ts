import type { Locale } from "./config";

/**
 * Error texts for Client Components (error.tsx / global-error.tsx). Kept tiny and separate from
 * the full dictionaries so client bundles don't ship every translation.
 */
export const errorMessages: Record<Locale, { title: string; text: string; retry: string }> = {
  ar: {
    title: "حدث خطأ",
    text: "تعذّر تحميل هذه الصفحة. تحقّق من الاتصال ثم أعد المحاولة.",
    retry: "إعادة المحاولة",
  },
  fr: {
    title: "Une erreur est survenue",
    text: "Impossible de charger cette page. Vérifiez votre connexion puis réessayez.",
    retry: "Réessayer",
  },
  en: {
    title: "Something went wrong",
    text: "This page could not be loaded. Check your connection and try again.",
    retry: "Try again",
  },
};
