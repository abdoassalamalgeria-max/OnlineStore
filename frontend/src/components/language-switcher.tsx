"use client";

import Link from "next/link";
import { usePathname, useSearchParams } from "next/navigation";
import { localeCookie, localeNames, locales, type Locale } from "@/i18n/config";

/**
 * Links to the same page in each language, keeping the query string (search, filters, page).
 * Remembers the choice in a cookie so the proxy redirects "/" to it next time.
 */
export function LanguageSwitcher({ current, label }: { current: Locale; label: string }) {
  const pathname = usePathname();
  const searchParams = useSearchParams();
  const rest = pathname.split("/").slice(2).join("/");
  const query = searchParams.toString();

  return (
    <nav aria-label={label}>
      <ul className="flex gap-3 text-sm">
        {locales.map((locale) => (
          <li key={locale}>
            <Link
              href={`/${locale}${rest ? `/${rest}` : ""}${query ? `?${query}` : ""}`}
              hrefLang={locale}
              lang={locale}
              aria-current={locale === current ? "true" : undefined}
              onClick={() => {
                document.cookie = `${localeCookie}=${locale}; path=/; max-age=31536000; samesite=lax`;
              }}
              className={
                locale === current ? "text-brand font-bold" : "text-muted hover:text-foreground"
              }
            >
              {localeNames[locale]}
            </Link>
          </li>
        ))}
      </ul>
    </nav>
  );
}
