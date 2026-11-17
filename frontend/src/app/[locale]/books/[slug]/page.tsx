import type { Metadata } from "next";
import Link from "next/link";
import { notFound } from "next/navigation";
import { Suspense } from "react";
import { AddToCartButton } from "@/components/add-to-cart-button";
import { BookCover } from "@/components/book-cover";
import { BookReviews } from "@/components/book-reviews";
import { Stars } from "@/components/stars";
import { Price } from "@/components/price";
import { locales, type Locale } from "@/i18n/config";
import { format, getDictionary, getLocale, type Dictionary } from "@/i18n/dictionaries";
import { getBook } from "@/lib/api/catalog";
import type { BookDetails } from "@/lib/api/client";
import { formatYear } from "@/lib/format";
import { localizedName } from "@/lib/localize";

/** Low-stock warning threshold, matching the admin dashboard. */
const lowStock = 5;

export async function generateMetadata({
  params,
}: PageProps<"/[locale]/books/[slug]">): Promise<Metadata> {
  const { slug } = await params;
  const book = await getBook(slug);
  if (!book) {
    return {};
  }

  const description = book.description.slice(0, 160);
  return {
    title: book.title,
    description,
    alternates: {
      canonical: `/${await getLocale()}/books/${book.slug}`,
      languages: Object.fromEntries(locales.map((l) => [l, `/${l}/books/${book.slug}`])),
    },
    openGraph: {
      type: "book",
      title: book.title,
      description,
      isbn: book.isbn,
      authors: book.authors.map((a) => a.name),
      images: book.coverImageUrl ? [book.coverImageUrl] : undefined,
    },
  };
}

/**
 * Book details. Not listed in generateStaticParams: the first visit to a slug gets the static
 * shell with the skeleton, then the rendered page is cached for later visitors (ISR).
 */
export default async function BookPage({ params }: PageProps<"/[locale]/books/[slug]">) {
  const t = await getDictionary();
  const locale = await getLocale();

  return (
    <div className="flex flex-col gap-6">
      <Link href={`/${locale}/books`} className="text-brand text-sm hover:underline">
        {locale === "ar" ? "→" : "←"} {t.book.backToCatalog}
      </Link>
      <Suspense fallback={<BookSkeleton label={t.loading} />}>
        <BookContent params={params} locale={locale} t={t} />
      </Suspense>
    </div>
  );
}

async function BookContent({
  params,
  locale,
  t,
}: {
  params: PageProps<"/[locale]/books/[slug]">["params"];
  locale: Locale;
  t: Dictionary;
}) {
  const { slug } = await params;
  const book = await getBook(slug);
  if (!book) {
    notFound();
  }

  const year = formatYear(book.publishedOn);
  const facts: [string, string | number][] = [
    [t.book.isbn, book.isbn],
    [t.book.pages, book.pageCount],
    [
      t.book.language,
      t.bookLanguages[book.language as keyof Dictionary["bookLanguages"]] ?? book.language,
    ],
    [t.book.publisher, book.publisher],
    ...(year ? ([[t.book.published, year]] as [string, string][]) : []),
  ];

  return (
    <article className="grid gap-8 md:grid-cols-[18rem_1fr]">
      <JsonLd book={book} locale={locale} />

      <div className="relative mx-auto aspect-[2/3] w-56 md:w-full">
        <BookCover
          title={book.title}
          url={book.coverImageUrl}
          language={book.language}
          priority
          sizes="18rem"
        />
      </div>

      <div className="flex flex-col gap-5">
        <header>
          <h1 lang={book.language} dir="auto" className="text-3xl font-bold">
            {book.title}
          </h1>
          <p className="text-muted mt-2">
            {t.book.by}{" "}
            {book.authors.map((author, i) => (
              <span key={author.id}>
                {i > 0 && (locale === "ar" ? "، " : ", ")}
                <Link
                  href={`/${locale}/books?author=${author.slug}`}
                  lang={book.language}
                  className="text-foreground hover:text-brand font-medium"
                >
                  {author.name}
                </Link>
              </span>
            ))}
          </p>
        </header>

        <div className="flex flex-col gap-2">
          <Price
            price={book.price}
            salePrice={book.salePrice}
            locale={locale}
            saleLabel={t.book.onSale}
            size="lg"
          />
          <StockBadge stock={book.stock} t={t} />
          {book.averageRating != null && (
            <a href="#reviews" className="flex w-fit items-center gap-2 text-sm hover:underline">
              <Stars
                value={book.averageRating}
                label={format(t.reviews.outOf5, { value: book.averageRating.toFixed(1) })}
              />
              <span className="text-muted">
                {format(t.reviews.count, { count: book.reviewCount })}
              </span>
            </a>
          )}
        </div>

        {book.inStock && (
          <AddToCartButton
            bookId={book.id}
            maxQuantity={book.stock}
            cartHref={`/${locale}/cart`}
            labels={{
              quantity: t.cart.quantity,
              addToCart: t.book.addToCart,
              adding: t.cart.adding,
              added: t.cart.added,
              viewCart: t.cart.viewCart,
              error: t.auth.genericError,
            }}
          />
        )}

        <section aria-labelledby="about">
          <h2 id="about" className="mb-2 text-xl font-bold">
            {t.book.description}
          </h2>
          <p lang={book.language} dir="auto" className="leading-relaxed">
            {book.description}
          </p>
        </section>

        <section aria-labelledby="details">
          <h2 id="details" className="mb-2 text-xl font-bold">
            {t.book.details}
          </h2>
          <dl className="grid grid-cols-[auto_1fr] gap-x-6 gap-y-2 text-sm">
            {facts.map(([label, value]) => (
              <div key={label} className="contents">
                <dt className="text-muted">{label}</dt>
                <dd>{value}</dd>
              </div>
            ))}
            <dt className="text-muted">{t.book.categories}</dt>
            <dd className="flex flex-wrap gap-2">
              {book.categories.map((c) => (
                <Link
                  key={c.id}
                  href={`/${locale}/books?category=${c.slug}`}
                  className="border-border hover:border-brand hover:text-brand rounded-full border px-2.5 py-0.5"
                >
                  {localizedName(c, locale)}
                </Link>
              ))}
            </dd>
          </dl>
        </section>

        <BookReviews slug={book.slug} locale={locale} t={t} />
      </div>
    </article>
  );
}

function StockBadge({ stock, t }: { stock: number; t: Dictionary }) {
  if (stock === 0) {
    return <p className="text-muted text-sm font-medium">{t.book.outOfStock}</p>;
  }
  if (stock <= lowStock) {
    return (
      <p className="text-accent text-sm font-medium">{format(t.book.lowStock, { count: stock })}</p>
    );
  }
  return <p className="text-brand text-sm font-medium">{t.book.inStock}</p>;
}

/** schema.org Book + Offer so search engines can show price and availability. */
function JsonLd({ book, locale }: { book: BookDetails; locale: Locale }) {
  const data = {
    "@context": "https://schema.org",
    "@type": "Book",
    name: book.title,
    isbn: book.isbn,
    inLanguage: book.language,
    numberOfPages: book.pageCount,
    author: book.authors.map((a) => ({ "@type": "Person", name: a.name })),
    publisher: { "@type": "Organization", name: book.publisher },
    image: book.coverImageUrl ?? undefined,
    aggregateRating:
      book.averageRating != null && book.reviewCount > 0
        ? {
            "@type": "AggregateRating",
            ratingValue: book.averageRating,
            reviewCount: book.reviewCount,
          }
        : undefined,
    offers: {
      "@type": "Offer",
      price: book.effectivePrice,
      priceCurrency: "DZD",
      availability: book.inStock ? "https://schema.org/InStock" : "https://schema.org/OutOfStock",
      url: `/${locale}/books/${book.slug}`,
    },
  };
  // Escape "<" so a title can never close the script tag.
  const json = JSON.stringify(data).replace(/</g, "\\u003c");
  return <script type="application/ld+json" dangerouslySetInnerHTML={{ __html: json }} />;
}

function BookSkeleton({ label }: { label: string }) {
  return (
    <div role="status" aria-label={label} className="grid gap-8 md:grid-cols-[18rem_1fr]">
      <div className="bg-border mx-auto aspect-[2/3] w-56 animate-pulse rounded-md md:w-full" />
      <div className="flex flex-col gap-4" aria-hidden="true">
        <div className="bg-border h-8 w-2/3 animate-pulse rounded" />
        <div className="bg-border h-4 w-1/3 animate-pulse rounded" />
        <div className="bg-border h-6 w-1/4 animate-pulse rounded" />
        <div className="bg-border h-24 w-full animate-pulse rounded" />
      </div>
    </div>
  );
}
