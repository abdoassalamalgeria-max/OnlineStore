import Link from "next/link";
import type { Locale } from "@/i18n/config";
import type { Dictionary } from "@/i18n/dictionaries";
import type { BookSummary } from "@/lib/api/client";
import { BookCover } from "./book-cover";
import { format } from "@/i18n/messages";
import { Price } from "./price";
import { Stars } from "./stars";

type Props = { book: BookSummary; locale: Locale; t: Dictionary };

export function BookCard({ book, locale, t }: Props) {
  const authors = book.authors.map((a) => a.name).join(locale === "ar" ? "، " : ", ");

  return (
    <article className="group border-border bg-surface flex h-full flex-col gap-3 rounded-lg border p-3 transition hover:shadow-md">
      <Link href={`/${locale}/books/${book.slug}`} className="flex flex-col gap-3">
        <div className="relative aspect-[2/3] w-full overflow-hidden rounded-md">
          <BookCover title={book.title} url={book.coverImageUrl} language={book.language} />
        </div>
        <h3
          lang={book.language}
          dir="auto"
          className="group-hover:text-brand line-clamp-2 font-bold"
        >
          {book.title}
        </h3>
      </Link>
      <p lang={book.language} dir="auto" className="text-muted line-clamp-1 text-sm">
        {authors}
      </p>
      {book.averageRating != null && (
        <p className="flex items-center gap-1 text-xs">
          <Stars
            value={book.averageRating}
            label={format(t.reviews.outOf5, { value: book.averageRating.toFixed(1) })}
          />
          <span className="text-muted">({book.reviewCount})</span>
        </p>
      )}
      <div className="mt-auto flex flex-wrap items-center justify-between gap-2">
        <Price
          price={book.price}
          salePrice={book.salePrice}
          locale={locale}
          saleLabel={t.book.onSale}
        />
        {!book.inStock && <span className="text-muted text-xs">{t.book.outOfStock}</span>}
      </div>
    </article>
  );
}

export function BookGrid({
  books,
  locale,
  t,
}: {
  books: BookSummary[];
  locale: Locale;
  t: Dictionary;
}) {
  return (
    <ul className="grid grid-cols-2 gap-4 sm:grid-cols-3 lg:grid-cols-4">
      {books.map((book) => (
        <li key={book.id}>
          <BookCard book={book} locale={locale} t={t} />
        </li>
      ))}
    </ul>
  );
}

/** Placeholder grid shown while books stream in. */
export function BookGridSkeleton({ count = 8, label }: { count?: number; label: string }) {
  return (
    <div role="status" aria-label={label}>
      <ul className="grid grid-cols-2 gap-4 sm:grid-cols-3 lg:grid-cols-4" aria-hidden="true">
        {Array.from({ length: count }, (_, i) => (
          <li
            key={i}
            className="border-border bg-surface flex flex-col gap-3 rounded-lg border p-3"
          >
            <div className="bg-border aspect-[2/3] w-full animate-pulse rounded-md" />
            <div className="bg-border h-4 w-3/4 animate-pulse rounded" />
            <div className="bg-border h-3 w-1/2 animate-pulse rounded" />
          </li>
        ))}
      </ul>
    </div>
  );
}
