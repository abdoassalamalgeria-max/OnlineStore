import type { Metadata } from "next";
import { Suspense } from "react";
import { LoginForm } from "@/components/auth-forms";
import { getDictionary, getLocale } from "@/i18n/dictionaries";
import { safeReturnTo } from "@/lib/actions/shared";

export async function generateMetadata(): Promise<Metadata> {
  const t = await getDictionary();
  return { title: t.auth.loginTitle, robots: { index: false } };
}

export default async function LoginPage({ searchParams }: PageProps<"/[locale]/login">) {
  const t = await getDictionary();

  return (
    <div className="mx-auto w-full max-w-sm">
      <h1 className="mb-6 text-2xl font-bold">{t.auth.loginTitle}</h1>
      <Suspense>
        <LoginFormWithReturn searchParams={searchParams} />
      </Suspense>
    </div>
  );
}

async function LoginFormWithReturn({
  searchParams,
}: Pick<PageProps<"/[locale]/login">, "searchParams">) {
  const t = await getDictionary();
  const locale = await getLocale();
  const { returnTo } = await searchParams;
  const target = safeReturnTo(typeof returnTo === "string" ? returnTo : null, locale);
  return <LoginForm locale={locale} returnTo={target} t={t.auth} loading={t.loading} />;
}
