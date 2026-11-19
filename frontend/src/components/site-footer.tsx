import { getDictionary } from "@/i18n/dictionaries";

export async function SiteFooter() {
  const t = await getDictionary();

  return (
    <footer className="bg-brand-strong mt-8 border-t-4 border-t-[var(--gold)]">
      <div className="mx-auto flex max-w-6xl flex-col gap-1 px-4 py-7 text-sm text-white/85 sm:flex-row sm:justify-between">
        <p>
          {t.meta.siteName}. {t.footer.rights}
        </p>
        <p>{t.footer.payments}</p>
      </div>
    </footer>
  );
}
