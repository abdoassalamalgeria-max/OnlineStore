import type { Metadata } from "next";
import { IBM_Plex_Sans_Arabic } from "next/font/google";
import { SiteFooter } from "@/components/site-footer";
import { SiteHeader } from "@/components/site-header";
import { directionOf, locales } from "@/i18n/config";
import { getDictionary, getLocale } from "@/i18n/dictionaries";
import "../globals.css";

// Arabic-first typeface that also covers Latin script (fr/en).
const plexArabic = IBM_Plex_Sans_Arabic({
  variable: "--font-plex-arabic",
  subsets: ["arabic", "latin"],
  weight: ["400", "500", "700"],
});

// One prerendered shell per UI language: /ar, /fr, /en.
export function generateStaticParams() {
  return locales.map((locale) => ({ locale }));
}

export async function generateMetadata(): Promise<Metadata> {
  const t = await getDictionary();
  return {
    metadataBase: new URL(process.env.SITE_URL ?? "http://localhost:3000"),
    title: { default: t.meta.siteName, template: `%s | ${t.meta.siteName}` },
    description: t.meta.tagline,
    openGraph: { siteName: t.meta.siteName, type: "website" },
  };
}

/**
 * Root layout. Sets <html lang/dir> from the [locale] segment so Arabic renders right-to-left;
 * use logical Tailwind utilities (ms-*, pe-*, start-*) so layouts mirror automatically.
 */
export default async function LocaleLayout({ children }: LayoutProps<"/[locale]">) {
  const locale = await getLocale();
  const t = await getDictionary();

  return (
    <html
      lang={locale}
      dir={directionOf(locale)}
      className={`${plexArabic.variable} h-full antialiased`}
    >
      <body className="bg-background text-foreground flex min-h-full flex-col">
        <a
          href="#main"
          className="focus:bg-brand sr-only focus:not-sr-only focus:fixed focus:start-4 focus:top-4 focus:z-50 focus:rounded focus:px-4 focus:py-2 focus:text-white"
        >
          {t.nav.skipToContent}
        </a>
        <SiteHeader />
        <main id="main" className="mx-auto w-full max-w-6xl flex-1 px-4 py-8">
          {children}
        </main>
        <SiteFooter />
      </body>
    </html>
  );
}
