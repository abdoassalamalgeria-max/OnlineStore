import type { Metadata } from "next";
import { Suspense } from "react";
import { RegisterForm } from "@/components/auth-forms";
import { getDictionary, getLocale } from "@/i18n/dictionaries";
import { safeReturnTo } from "@/lib/actions/shared";

export async function generateMetadata(): Promise<Metadata> {
  const t = await getDictionary();
  return { title: t.auth.registerTitle, robots: { index: false } };
}

export default async function RegisterPage({ searchParams }: PageProps<"/[locale]/register">) {
  const t = await getDictionary();

  return (
    <div className="mx-auto w-full max-w-sm">
      <h1 className="mb-6 text-2xl font-bold">{t.auth.registerTitle}</h1>
      <Suspense>
        <RegisterFormWithReturn searchParams={searchParams} />
      </Suspense>
    </div>
  );
}

async function RegisterFormWithReturn({
  searchParams,
}: Pick<PageProps<"/[locale]/register">, "searchParams">) {
  const t = await getDictionary();
  const locale = await getLocale();
  const { returnTo } = await searchParams;
  const target = safeReturnTo(typeof returnTo === "string" ? returnTo : null, locale);
  return <RegisterForm locale={locale} returnTo={target} t={t.auth} loading={t.loading} />;
}
