import Link from "next/link";
import { getDictionary, getLocale } from "@/i18n/dictionaries";

export default async function NotFound() {
  const t = await getDictionary();
  const locale = await getLocale();

  return (
    <div className="flex flex-col items-center gap-4 py-20 text-center">
      <h1 className="text-3xl font-bold">{t.errors.notFoundTitle}</h1>
      <p className="text-muted">{t.errors.notFoundText}</p>
      <Link
        href={`/${locale}`}
        className="bg-brand hover:bg-brand-strong rounded-md px-5 py-2.5 font-medium text-white"
      >
        {t.errors.goHome}
      </Link>
    </div>
  );
}
