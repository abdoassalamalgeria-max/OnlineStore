import type { Metadata } from "next";
import Link from "next/link";
import { Suspense } from "react";
import { PasswordForm, ProfileForm } from "@/components/account-forms";
import { getDictionary, getLocale } from "@/i18n/dictionaries";
import { ApiError } from "@/lib/api/client";
import { userApi } from "@/lib/api/server-client";
import { requireSignedIn } from "@/lib/auth/require-user";

export async function generateMetadata(): Promise<Metadata> {
  const t = await getDictionary();
  return { title: t.account.title, robots: { index: false } };
}

export default async function AccountPage() {
  const t = await getDictionary();
  const locale = await getLocale();

  return (
    <div className="flex flex-col gap-8">
      <div className="flex flex-wrap items-baseline justify-between gap-4">
        <h1 className="text-3xl font-bold">{t.account.title}</h1>
        <Link href={`/${locale}/account/orders`} className="text-brand font-medium underline">
          {t.account.orders}
        </Link>
      </div>
      <Suspense fallback={<p className="text-muted">{t.loading}</p>}>
        <AccountContent />
      </Suspense>
    </div>
  );
}

async function AccountContent() {
  const t = await getDictionary();
  const locale = await getLocale();
  await requireSignedIn(locale, `/${locale}/account`);

  const api = await userApi();
  const { data: me, response } = await api.GET("/api/v1/auth/me");
  if (!me) throw new ApiError(response.status, "/api/v1/auth/me");

  return (
    <div className="grid gap-8 md:grid-cols-2">
      <section aria-labelledby="profile" className="border-border bg-surface rounded-lg border p-5">
        <h2 id="profile" className="mb-4 text-lg font-bold">
          {t.account.profile}
        </h2>
        <ProfileForm
          locale={locale}
          t={t}
          loading={t.loading}
          defaults={{ fullName: me.fullName, phoneNumber: me.phoneNumber ?? null, email: me.email }}
        />
      </section>
      <section
        aria-labelledby="password"
        className="border-border bg-surface rounded-lg border p-5"
      >
        <h2 id="password" className="mb-4 text-lg font-bold">
          {t.account.passwordTitle}
        </h2>
        <PasswordForm locale={locale} t={t} loading={t.loading} />
      </section>
    </div>
  );
}
