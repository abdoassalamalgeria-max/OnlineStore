import Link from "next/link";
import { Suspense } from "react";
import { getDictionary, getLocale } from "@/i18n/dictionaries";
import { ApiError } from "@/lib/api/client";
import { adminApi } from "@/lib/api/server-client";
import { formatDzd } from "@/lib/format";

export default async function AdminOverviewPage() {
  const t = await getDictionary();
  return (
    <div className="flex flex-col gap-6">
      <h1 className="text-3xl font-bold">{t.admin.nav.overview}</h1>
      <Suspense fallback={<p className="text-muted">{t.loading}</p>}>
        <Overview />
      </Suspense>
    </div>
  );
}

async function Overview() {
  const t = await getDictionary();
  const locale = await getLocale();
  const api = await adminApi();
  const { data, response } = await api.GET("/api/v1/admin/overview");
  if (!data) throw new ApiError(response.status, "/api/v1/admin/overview");

  const s = t.admin.stats;
  const cards: { label: string; value: string | number; href?: string; tone?: "warn" }[] = [
    { label: s.toShip, value: data.toShip, href: `/${locale}/admin/orders?status=Paid` },
    {
      label: s.attention,
      value: data.requiringAttention,
      href: `/${locale}/admin/orders?attention=1`,
      tone: data.requiringAttention ? "warn" : undefined,
    },
    { label: s.revenue30, value: formatDzd(data.revenueLast30Days, locale) },
    { label: s.revenue, value: formatDzd(data.revenue, locale) },
    { label: s.books, value: data.books, href: `/${locale}/admin/books` },
    {
      label: s.lowStock,
      value: data.lowStockBooks,
      href: `/${locale}/admin/books?lowStock=1`,
      tone: data.lowStockBooks ? "warn" : undefined,
    },
    {
      label: s.outOfStock,
      value: data.outOfStockBooks,
      tone: data.outOfStockBooks ? "warn" : undefined,
    },
    { label: s.authors, value: data.authors, href: `/${locale}/admin/authors` },
  ];

  return (
    <div className="flex flex-col gap-8">
      <ul className="grid grid-cols-2 gap-4 lg:grid-cols-4">
        {cards.map((card) => {
          const body = (
            <>
              <span className="text-muted text-sm">{card.label}</span>
              <span className={`text-2xl font-bold ${card.tone === "warn" ? "text-accent" : ""}`}>
                {card.value}
              </span>
            </>
          );
          return (
            <li key={card.label} className="border-border bg-surface rounded-lg border">
              {card.href ? (
                <Link
                  href={card.href}
                  className="hover:border-brand flex flex-col gap-1 rounded-lg p-4"
                >
                  {body}
                </Link>
              ) : (
                <div className="flex flex-col gap-1 p-4">{body}</div>
              )}
            </li>
          );
        })}
      </ul>

      <div className="grid gap-6 md:grid-cols-2">
        <section
          aria-labelledby="by-status"
          className="border-border bg-surface rounded-lg border p-4"
        >
          <h2 id="by-status" className="mb-3 font-bold">
            {s.ordersByStatus}
          </h2>
          <dl className="grid grid-cols-[1fr_auto] gap-y-1 text-sm">
            {(["Pending", "Paid", "Shipped", "Delivered", "Cancelled", "Failed"] as const).map(
              (status) => (
                <div key={status} className="contents">
                  <dt>
                    <Link
                      href={`/${locale}/admin/orders?status=${status}`}
                      className="hover:text-brand"
                    >
                      {t.order.status[status]}
                    </Link>
                  </dt>
                  <dd className="font-medium">{data.ordersByStatus[status] ?? 0}</dd>
                </div>
              ),
            )}
          </dl>
        </section>

        <section
          aria-labelledby="low-stock"
          className="border-border bg-surface rounded-lg border p-4"
        >
          <h2 id="low-stock" className="mb-3 font-bold">
            {s.lowStockList}
          </h2>
          {data.lowStock.length === 0 ? (
            <p className="text-muted text-sm">{t.admin.noItems}</p>
          ) : (
            <ul className="flex flex-col gap-1 text-sm">
              {data.lowStock.map((book) => (
                <li key={book.id} className="flex justify-between gap-3">
                  <Link
                    href={`/${locale}/admin/books/${book.id}`}
                    dir="auto"
                    className="hover:text-brand truncate"
                  >
                    {book.title}
                  </Link>
                  <span className={book.stock === 0 ? "text-accent font-bold" : "font-medium"}>
                    {book.stock}
                  </span>
                </li>
              ))}
            </ul>
          )}
        </section>
      </div>
    </div>
  );
}
