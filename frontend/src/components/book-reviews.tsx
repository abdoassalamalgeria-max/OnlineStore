import Link from "next/link";
import { Suspense } from "react";
import type { Locale } from "@/i18n/config";
import { format, type Dictionary } from "@/i18n/messages";
import { getReviews } from "@/lib/api/catalog";
import { userApi } from "@/lib/api/server-client";
import { getAccessToken } from "@/lib/auth/session";
import { intlLocale } from "@/i18n/config";
import { ReviewForm } from "./review-form";
import { Stars } from "./stars";

type Props = { slug: string; locale: Locale; t: Dictionary };

/** Reviews section of the book page: public summary + list (cached), and the visitor's own review area. */
export async function BookReviews({ slug, locale, t }: Props) {
  const r = t.reviews;
  const data = await getReviews(slug);
  if (!data) return null;

  const { summary, reviews } = data;
  const dates = new Intl.DateTimeFormat(intlLocale[locale], { dateStyle: "medium" });

  return (
    <section aria-labelledby="reviews" className="flex flex-col gap-4">
      <h2 id="reviews" className="text-xl font-bold">
        {r.title}
      </h2>

      {summary.average !== null && summary.average !== undefined ? (
        <div className="flex flex-wrap items-center gap-6">
          <div className="flex items-center gap-3">
            <span className="text-3xl font-bold">{summary.average.toFixed(1)}</span>
            <div className="flex flex-col">
              <Stars
                value={summary.average}
                label={format(r.outOf5, { value: summary.average.toFixed(1) })}
                size="lg"
              />
              <span className="text-muted text-sm">
                {format(r.count, { count: summary.count })}
              </span>
            </div>
          </div>
          <ul className="flex flex-col gap-0.5 text-xs" aria-label={r.rating}>
            {[5, 4, 3, 2, 1].map((star) => {
              const n = summary.distribution[star - 1] ?? 0;
              return (
                <li key={star} className="flex items-center gap-2" dir="ltr">
                  <span className="w-3">{star}</span>
                  <span className="bg-border h-2 w-32 overflow-hidden rounded" aria-hidden="true">
                    <span
                      className="bg-accent block h-full"
                      style={{ width: `${summary.count ? (n / summary.count) * 100 : 0}%` }}
                    />
                  </span>
                  <span className="text-muted">{n}</span>
                </li>
              );
            })}
          </ul>
        </div>
      ) : (
        <p className="text-muted text-sm">{r.none}</p>
      )}

      {/* Per-visitor: reads the session cookie, so it streams separately from the cached list. */}
      <Suspense fallback={null}>
        <MyReview slug={slug} locale={locale} t={t} />
      </Suspense>

      {reviews.items.length > 0 && (
        <ul className="divide-border flex flex-col divide-y">
          {reviews.items.map((review) => (
            <li key={review.id} className="flex flex-col gap-1 py-3">
              <div className="flex flex-wrap items-center gap-3">
                <Stars value={review.rating} label={format(r.outOf5, { value: review.rating })} />
                <span className="font-medium">{review.authorName}</span>
                <time dateTime={review.createdAt} className="text-muted text-xs">
                  {dates.format(new Date(review.createdAt))}
                </time>
              </div>
              {review.comment && (
                <p dir="auto" className="leading-relaxed whitespace-pre-line">
                  {review.comment}
                </p>
              )}
            </li>
          ))}
        </ul>
      )}
    </section>
  );
}

async function MyReview({ slug, locale, t }: Props) {
  if (!(await getAccessToken())) {
    return (
      <Link
        href={`/${locale}/login?returnTo=${encodeURIComponent(`/${locale}/books/${slug}`)}`}
        className="text-brand w-fit text-sm font-medium underline"
      >
        {t.reviews.signInToReview}
      </Link>
    );
  }

  const api = await userApi();
  const { data } = await api.GET("/api/v1/books/{slug}/reviews/mine", {
    params: { path: { slug } },
  });
  if (!data) return null;

  if (!data.canReview && !data.review) {
    return <p className="text-muted text-sm">{t.reviews.buyersOnly}</p>;
  }

  return (
    <ReviewForm
      slug={slug}
      initial={
        data.review ? { rating: data.review.rating, comment: data.review.comment ?? null } : null
      }
      t={t.reviews}
      loading={t.loading}
      genericError={t.auth.genericError}
    />
  );
}
