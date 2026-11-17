import Link from "next/link";
import { connection } from "next/server";
import { Suspense } from "react";
import { BookGrid, BookGridSkeleton } from "@/components/book-card";
import { getDictionary, getLocale, type Dictionary } from "@/i18n/dictionaries";
import { getBooks, type BookFilters } from "@/lib/api/catalog";

export default async function HomePage() {
  const locale = await getLocale();
  const t = await getDictionary();

  return (
    <div className="flex flex-col gap-12">
      {/* Static hero: part of the prerendered shell. */}
      <section className="bg-brand rounded-2xl px-6 py-12 text-white sm:px-12">
        <h1 className="text-3xl font-bold sm:text-4xl">{t.home.heroTitle}</h1>
        <p className="mt-3 max-w-2xl text-lg text-white/90">{t.home.heroText}</p>
        <Link
          href={`/${locale}/books`}
          className="text-brand mt-6 inline-block rounded-md bg-white px-5 py-2.5 font-medium hover:bg-white/90"
        >
          {t.home.browseAll}
        </Link>
      </section>

      <BookSection
        title={t.home.featured}
        viewAllHref={`/${locale}/books`}
        filters={{ featured: true, pageSize: 8 }}
        t={t}
      />
      <BookSection
        title={t.home.newReleases}
        viewAllHref={`/${locale}/books`}
        filters={{ sort: "Newest", pageSize: 8 }}
        t={t}
      />

      <section aria-labelledby="best-sellers">
        <h2 id="best-sellers" className="mb-4 text-2xl font-bold">
          {t.home.bestSellers}
        </h2>
        <p className="border-border text-muted rounded-lg border border-dashed p-6 text-center">
          {t.home.bestSellersSoon}
        </p>
      </section>
    </div>
  );
}

type SectionProps = { title: string; viewAllHref: string; filters: BookFilters; t: Dictionary };

function BookSection({ title, viewAllHref, filters, t }: SectionProps) {
  const id = title.replace(/\s+/g, "-");
  return (
    <section aria-labelledby={id}>
      <div className="mb-4 flex items-baseline justify-between">
        <h2 id={id} className="text-2xl font-bold">
          {title}
        </h2>
        <Link href={viewAllHref} className="text-brand text-sm font-medium hover:underline">
          {t.home.viewAll}
        </Link>
      </div>
      <Suspense fallback={<BookGridSkeleton label={t.loading} />}>
        <SectionBooks filters={filters} t={t} />
      </Suspense>
    </section>
  );
}

async function SectionBooks({ filters, t }: { filters: BookFilters; t: Dictionary }) {
  // Fetch at request time (not at build time, when the API may be offline); getBooks itself
  // is cached for a few minutes, so this does not hit the API on every visit.
  await connection();
  const locale = await getLocale();

  // Only the data fetch is guarded; a failing section should not take the whole home page down.
  const books = await getBooks(filters).then(
    (result) => result.items,
    () => null,
  );

  if (books === null) {
    return (
      <p role="alert" className="border-border text-muted rounded-lg border p-6 text-center">
        {t.errors.text}
      </p>
    );
  }

  return <BookGrid books={books} locale={locale} t={t} />;
}
