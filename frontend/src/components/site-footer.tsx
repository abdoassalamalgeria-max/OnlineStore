import { getDictionary } from "@/i18n/dictionaries";

export async function SiteFooter() {
  const t = await getDictionary();

  return (
    <footer className="border-border bg-surface border-t">
      <div className="text-muted mx-auto flex max-w-6xl flex-col gap-1 px-4 py-6 text-sm sm:flex-row sm:justify-between">
        <p>
          {t.meta.siteName}. {t.footer.rights}
        </p>
        <p>{t.footer.payments}</p>
      </div>
    </footer>
  );
}
