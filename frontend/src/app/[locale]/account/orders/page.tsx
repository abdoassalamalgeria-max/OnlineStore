import type { Metadata } from "next";
import Link from "next/link";
import { Suspense } from "react";
import { OrderStatusBadge } from "@/components/order-status-badge";
import { Pagination } from "@/components/pagination";
import { intlLocale } from "@/i18n/config";
import { format, getDictionary, getLocale } from "@/i18n/dictionaries";
import { ApiError } from "@/lib/api/client";
import { userApi } from "@/lib/api/server-client";
import { requireSignedIn } from "@/lib/auth/require-user";
import { formatDzd } from "@/lib/format";

export async function generateMetadata(): Promise<Metadata> {
  const t = await getDictionary();
  return { title: t.account.orders, robots: { index: false } };
}

export default async function OrdersPage({ searchParams }: PageProps<"/[locale]/account/orders">) {
  const t = await getDictionary();
  return (
    <div className="flex flex-col gap-6">
      <h1 className="text-3xl font-bold">{t.account.orders}</h1>
      <Suspense fallback={<p className="text-muted">{t.loading}</p>}>
        <OrdersList searchParams={searchParams} />
      </Suspense>
    </div>
  );
}

async function OrdersList({
  searchParams,
}: Pick<PageProps<"/[locale]/account/orders">, "searchParams">) {
  const t = await getDictionary();
  const locale = await getLocale();
  await requireSignedIn(locale, `/${locale}/account/orders`);

  const { page: rawPage } = await searchParams;
  const page = Math.max(1, Number(typeof rawPage === "string" ? rawPage : 1) || 1);
  const api = await userApi();
  const { data, response } = await api.GET("/api/v1/orders", {
    params: { query: { page, pageSize: 10 } },
  });
  if (!data) throw new ApiError(response.status, "/api/v1/orders");

  if (data.items.length === 0) {
    return (
      <div className="border-border flex flex-col items-center gap-3 rounded-lg border border-dashed p-10 text-center">
        <p>{t.account.noOrders}</p>
        <Link href={`/${locale}/books`} className="text-brand font-medium underline">
          {t.cart.continueShopping}
        </Link>
      </div>
    );
  }

  const dates = new Intl.DateTimeFormat(intlLocale[locale], { dateStyle: "medium" });

  return (
    <>
      <ul className="divide-border border-border bg-surface divide-y rounded-lg border">
        {data.items.map((order) => (
          <li key={order.id} className="flex flex-wrap items-center justify-between gap-3 p-4">
            <div className="flex flex-col gap-1">
              <div className="flex flex-wrap items-center gap-3">
                <span dir="ltr" className="font-mono font-bold">
                  {order.orderNumber}
                </span>
                <OrderStatusBadge status={order.status} t={t} />
              </div>
              <p className="text-muted text-sm">
                <time dateTime={order.placedAt}>{dates.format(new Date(order.placedAt))}</time> ·{" "}
                {format(t.account.itemsCount, { count: order.itemCount })}
              </p>
              <p className="line-clamp-1 text-sm">
                {order.titles.join(locale === "ar" ? "، " : ", ")}
              </p>
            </div>
            <div className="flex items-center gap-4">
              <span className="font-bold">{formatDzd(order.total, locale)}</span>
              <Link
                href={`/${locale}/account/orders/${order.id}`}
                className="text-brand text-sm font-medium underline"
              >
                {t.account.viewOrder}
              </Link>
            </div>
          </li>
        ))}
      </ul>
      <Pagination
        page={data.page}
        totalPages={data.totalPages ?? 0}
        hrefFor={(p) => `/${locale}/account/orders${p > 1 ? `?page=${p}` : ""}`}
        t={t}
      />
    </>
  );
}
