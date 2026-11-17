import Link from "next/link";
import { Suspense } from "react";
import { getDictionary, getLocale } from "@/i18n/dictionaries";
import { localeNames, locales } from "@/i18n/config";
import { AccountNav } from "./account-nav";
import { LanguageSwitcher } from "./language-switcher";

export async function SiteHeader() {
  const locale = await getLocale();
  const t = await getDictionary();

  return (
    <header className="border-border bg-surface border-b">
      <div className="mx-auto flex max-w-6xl flex-wrap items-center gap-x-6 gap-y-3 px-4 py-3">
        <Link href={`/${locale}`} className="text-brand text-xl font-bold">
          {t.meta.siteName}
        </Link>

        <nav aria-label={t.nav.mainNav} className="flex gap-4 text-sm font-medium">
          <Link href={`/${locale}`} className="hover:text-brand">
            {t.nav.home}
          </Link>
          <Link href={`/${locale}/books`} className="hover:text-brand">
            {t.nav.catalog}
          </Link>
        </nav>

        <form
          action={`/${locale}/books`}
          method="get"
          role="search"
          className="order-last flex w-full gap-2 sm:order-none sm:ms-auto sm:w-auto"
        >
          <label htmlFor="header-search" className="sr-only">
            {t.catalog.searchLabel}
          </label>
          <input
            id="header-search"
            name="q"
            type="search"
            placeholder={t.catalog.searchPlaceholder}
            className="border-border bg-background w-full rounded-md border px-3 py-1.5 text-sm sm:w-64"
          />
          <button
            type="submit"
            className="bg-brand hover:bg-brand-strong rounded-md px-3 py-1.5 text-sm font-medium text-white"
          >
            {t.catalog.searchButton}
          </button>
        </form>

        {/* Reads the current URL, so it renders at request time; the fallback keeps the shell static. */}
        <Suspense
          fallback={
            <span className="text-muted text-sm">
              {locales.map((l) => localeNames[l]).join(" · ")}
            </span>
          }
        >
          <LanguageSwitcher current={locale} label={t.nav.language} />
        </Suspense>

        {/* Session-specific: streams per request while the rest of the header stays in the static shell. */}
        <Suspense
          fallback={
            <Link href={`/${locale}/cart`} className="text-sm font-medium">
              {t.nav.cart}
            </Link>
          }
        >
          <AccountNav />
        </Suspense>
      </div>
    </header>
  );
}
