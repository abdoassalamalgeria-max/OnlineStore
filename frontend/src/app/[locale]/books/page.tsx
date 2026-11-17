import type { Metadata } from "next";
import { Suspense } from "react";
import { BookGrid, BookGridSkeleton } from "@/components/book-card";
import { CatalogFilters } from "@/components/catalog-filters";
import { Pagination } from "@/components/pagination";
import { locales } from "@/i18n/config";
import { format, getDictionary, getLocale, type Dictionary } from "@/i18n/dictionaries";
import { getAuthors, getBooks, getCategories } from "@/lib/api/catalog";
import { catalogHref, parseCatalogParams } from "@/lib/catalog-params";

export async function generateMetadata(): Promise<Metadata> {
  const t = await getDictionary();
  const locale = await getLocale();
  return {
    title: t.meta.catalogTitle,
    description: t.meta.catalogDescription,
    alternates: {
      canonical: `/${locale}/books`,
      languages: Object.fromEntries(locales.map((l) => [l, `/${l}/books`])),
    },
  };
}

/**
 * Catalog. The heading is static; everything that depends on the query string streams in
 * behind <Suspense>. Filters are a plain GET form, so they work without JavaScript and every
 * result page has a shareable, crawlable URL.
 */
export default async function CatalogPage({ searchParams }: PageProps<"/[locale]/books">) {
  const t = await getDictionary();

  return (
    <div className="flex flex-col gap-6">
      <h1 className="text-3xl font-bold">{t.meta.catalogTitle}</h1>
      <Suspense fallback={<BookGridSkeleton count={12} label={t.loading} />}>
        <CatalogResults searchParams={searchParams} t={t} />
      </Suspense>
    </div>
  );
}

async function CatalogResults({
  searchParams,
  t,
}: {
  searchParams: PageProps<"/[locale]/books">["searchParams"];
  t: Dictionary;
}) {
  const locale = await getLocale();
  const filters = parseCatalogParams(await searchParams);
  const [result, categories, authors] = await Promise.all([
    getBooks(filters),
    getCategories(),
    getAuthors(),
  ]);

  return (
    <div className="grid gap-8 lg:grid-cols-[16rem_1fr]">
      <CatalogFilters
        locale={locale}
        filters={filters}
        categories={categories}
        authors={authors}
        t={t}
      />

      <section aria-labelledby="results-heading" aria-live="polite">
        <h2 id="results-heading" className="text-muted mb-4 text-sm">
          {format(t.catalog.results, { count: result.totalCount })}
        </h2>

        {result.items.length === 0 ? (
          <div className="border-border rounded-lg border border-dashed p-10 text-center">
            <p className="font-medium">{t.catalog.noResults}</p>
            <p className="text-muted mt-1 text-sm">{t.catalog.noResultsHint}</p>
          </div>
        ) : (
          <>
            <BookGrid books={result.items} locale={locale} t={t} />
            <Pagination
              page={result.page}
              totalPages={result.totalPages ?? 0}
              hrefFor={(page) => catalogHref(locale, filters, { page })}
              t={t}
            />
          </>
        )}
      </section>
    </div>
  );
}
