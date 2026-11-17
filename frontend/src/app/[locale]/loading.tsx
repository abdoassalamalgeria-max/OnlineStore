import { getDictionary } from "@/i18n/dictionaries";

/** Shown while a route segment under /[locale] loads (navigation between pages). */
export default async function Loading() {
  const t = await getDictionary();
  return (
    <div role="status" className="text-muted flex justify-center py-20">
      <span className="animate-pulse">{t.loading}</span>
    </div>
  );
}
