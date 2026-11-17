import Link from "next/link";
import { Suspense } from "react";
import { Pagination } from "@/components/pagination";
import { Stars } from "@/components/stars";
import { intlLocale } from "@/i18n/config";
import { format, getDictionary, getLocale } from "@/i18n/dictionaries";
import { deleteReviewAsAdmin } from "@/lib/actions/admin";
import { ApiError } from "@/lib/api/client";
import { adminApi } from "@/lib/api/server-client";

export default async function AdminReviewsPage({
  searchParams,
}: PageProps<"/[locale]/admin/reviews">) {
  const t = await getDictionary();
  return (
    <div className="flex flex-col gap-6">
      <h1 className="text-3xl font-bold">{t.admin.nav.reviews}</h1>
      <Suspense fallback={<p className="text-muted">{t.loading}</p>}>
        <Reviews searchParams={searchParams} />
      </Suspense>
    </div>
  );
}

async function Reviews({
  searchParams,
}: Pick<PageProps<"/[locale]/admin/reviews">, "searchParams">) {
  const t = await getDictionary();
  const locale = await getLocale();
  const page = Math.max(1, Number((await searchParams).page) || 1);
  const api = await adminApi();
  const { data, response } = await api.GET("/api/v1/admin/reviews", {
    params: { query: { page, pageSize: 20 } },
  });
  if (!data) throw new ApiError(response.status, "/api/v1/admin/reviews");
  const dates = new Intl.DateTimeFormat(intlLocale[locale], { dateStyle: "medium" });

  if (data.items.length === 0) return <p className="text-muted">{t.admin.noItems}</p>;

  return (
    <>
      <ul className="divide-border border-border bg-surface divide-y rounded-lg border">
        {data.items.map((review) => (
          <li key={review.id} className="flex flex-col gap-2 p-4">
            <div className="flex flex-wrap items-center gap-3 text-sm">
              <Stars
                value={review.rating}
                label={format(t.reviews.outOf5, { value: review.rating })}
              />
              <span className="font-medium">{review.authorName}</span>
              <span className="text-muted">{dates.format(new Date(review.createdAt))}</span>
              <Link
                href={`/${locale}/books/${review.bookSlug}`}
                dir="auto"
                className="text-brand underline"
              >
                {review.bookTitle}
              </Link>
            </div>
            {review.comment && (
              <p dir="auto" className="whitespace-pre-line">
                {review.comment}
              </p>
            )}
            <form action={deleteReviewAsAdmin}>
              <input type="hidden" name="locale" value={locale} />
              <input type="hidden" name="id" value={review.id} />
              <input type="hidden" name="slug" value={review.bookSlug} />
              <button type="submit" className="text-accent text-sm underline">
                {t.admin.delete}
              </button>
            </form>
          </li>
        ))}
      </ul>
      <Pagination
        page={data.page}
        totalPages={data.totalPages ?? 0}
        hrefFor={(p) => `/${locale}/admin/reviews${p > 1 ? `?page=${p}` : ""}`}
        t={t}
      />
    </>
  );
}
