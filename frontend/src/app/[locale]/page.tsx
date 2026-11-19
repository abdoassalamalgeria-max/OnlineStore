import Link from "next/link";
import { connection } from "next/server";
import { Suspense } from "react";
import { BookGrid, BookGridSkeleton } from "@/components/book-card";
import { HeroSlider, type Slide } from "@/components/hero-slider";
import { directionOf } from "@/i18n/config";
import { getDictionary, getLocale, type Dictionary } from "@/i18n/dictionaries";
import { getBooks, type BookFilters } from "@/lib/api/catalog";

export default async function HomePage() {
  const locale = await getLocale();
  const t = await getDictionary();

  const h = t.home;
  // Illustrations live in public/slides/ (regenerate with `node scripts/generate-slides.mjs`, or drop in
  // your own 1920×640 images with the same names).
  const slides: Slide[] = [
    {
      image: "/slides/shelves.svg",
      title: h.heroTitle,
      text: h.heroText,
      cta: { href: `/${locale}/books`, label: h.browseAll },
    },
    {
      image: "/slides/literature.svg",
      title: h.slide2Title,
      text: h.slide2Text,
      cta: { href: `/${locale}/books?category=algerian-literature`, label: h.slide2Cta },
    },
    {
      image: "/slides/delivery.svg",
      title: h.slide3Title,
      text: h.slide3Text,
      cta: { href: `/${locale}/books?inStock=1`, label: h.slide3Cta },
    },
  ];

  return (
    <div className="flex flex-col gap-12">
      {/* Static: part of the prerendered shell. -mt-8 cancels <main>'s top padding so it meets the header. */}
      <div className="-mt-8">
        <h1 className="sr-only">{t.meta.siteName}</h1>
        <HeroSlider
          slides={slides}
          dir={directionOf(locale)}
          labels={{
            carousel: h.sliderLabel,
            previous: h.slidePrevious,
            next: h.slideNext,
            goTo: h.slideGoTo,
            pause: h.slidePause,
            play: h.slidePlay,
          }}
        />
      </div>

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
