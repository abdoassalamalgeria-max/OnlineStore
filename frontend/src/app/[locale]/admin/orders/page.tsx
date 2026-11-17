import Link from "next/link";
import { Suspense } from "react";
import { inputClass } from "@/components/admin/admin-messages";
import { OrderStatusBadge } from "@/components/order-status-badge";
import { Pagination } from "@/components/pagination";
import { intlLocale } from "@/i18n/config";
import { getDictionary, getLocale } from "@/i18n/dictionaries";
import { ApiError, type Schemas } from "@/lib/api/client";
import { adminApi } from "@/lib/api/server-client";
import { formatDzd } from "@/lib/format";

const statuses: Schemas["OrderStatus"][] = [
  "Pending",
  "Paid",
  "Shipped",
  "Delivered",
  "Cancelled",
  "Failed",
];

export default async function AdminOrdersPage({
  searchParams,
}: PageProps<"/[locale]/admin/orders">) {
  const t = await getDictionary();
  return (
    <div className="flex flex-col gap-6">
      <h1 className="text-3xl font-bold">{t.admin.nav.orders}</h1>
      <Suspense fallback={<p className="text-muted">{t.loading}</p>}>
        <Orders searchParams={searchParams} />
      </Suspense>
    </div>
  );
}

async function Orders({ searchParams }: Pick<PageProps<"/[locale]/admin/orders">, "searchParams">) {
  const t = await getDictionary();
  const locale = await getLocale();
  const params = await searchParams;
  const status = statuses.find((s) => s === params.status);
  const search = typeof params.q === "string" ? params.q : "";
  const attention = params.attention === "1";
  const page = Math.max(1, Number(params.page) || 1);

  const api = await adminApi();
  const { data, response } = await api.GET("/api/v1/admin/orders", {
    params: {
      query: {
        status,
        search: search || undefined,
        requiresAttention: attention || undefined,
        page,
        pageSize: 20,
      },
    },
  });
  if (!data) throw new ApiError(response.status, "/api/v1/admin/orders");

  const dates = new Intl.DateTimeFormat(intlLocale[locale], {
    dateStyle: "short",
    timeStyle: "short",
  });
  const o = t.admin.orders;
  const href = (p: number) => {
    const q = new URLSearchParams();
    if (status) q.set("status", status);
    if (search) q.set("q", search);
    if (attention) q.set("attention", "1");
    if (p > 1) q.set("page", String(p));
    return `/${locale}/admin/orders${q.size ? `?${q}` : ""}`;
  };

  return (
    <>
      <form method="get" className="flex flex-wrap items-center gap-3">
        <label htmlFor="order-status" className="sr-only">
          {t.admin.allStatuses}
        </label>
        <select
          id="order-status"
          name="status"
          defaultValue={status ?? ""}
          className={`${inputClass} w-auto`}
        >
          <option value="">{t.admin.allStatuses}</option>
          {statuses.map((s) => (
            <option key={s} value={s}>
              {t.order.status[s]}
            </option>
          ))}
        </select>
        <label htmlFor="order-search" className="sr-only">
          {t.admin.searchOrders}
        </label>
        <input
          id="order-search"
          name="q"
          defaultValue={search}
          placeholder={t.admin.searchOrders}
          className={`${inputClass} max-w-xs`}
        />
        <label className="flex items-center gap-2 text-sm">
          <input type="checkbox" name="attention" value="1" defaultChecked={attention} />
          {t.admin.attentionOnly}
        </label>
        <button type="submit" className="border-border rounded-md border px-4 py-2 text-sm">
          {t.admin.search}
        </button>
      </form>

      {data.items.length === 0 ? (
        <p className="text-muted">{t.admin.noItems}</p>
      ) : (
        <div className="overflow-x-auto">
          <table className="w-full min-w-[48rem] text-sm">
            <thead className="text-muted border-border border-b">
              <tr>
                <th scope="col" className="p-2 text-start">
                  {t.order.number}
                </th>
                <th scope="col" className="p-2 text-start">
                  {o.placed}
                </th>
                <th scope="col" className="p-2 text-start">
                  {o.customer}
                </th>
                <th scope="col" className="p-2 text-end">
                  {o.total}
                </th>
                <th scope="col" className="p-2 text-start">
                  {t.admin.orders.attention}
                </th>
              </tr>
            </thead>
            <tbody>
              {data.items.map((order) => (
                <tr key={order.id} className="border-border border-b">
                  <td className="p-2">
                    <Link
                      href={`/${locale}/admin/orders/${order.id}`}
                      dir="ltr"
                      className="text-brand font-mono underline"
                    >
                      {order.orderNumber}
                    </Link>
                  </td>
                  <td className="p-2 whitespace-nowrap">
                    {dates.format(new Date(order.placedAt))}
                  </td>
                  <td className="p-2">
                    {order.customerName}
                    <span dir="ltr" className="text-muted block text-xs">
                      {order.phone} · {order.wilayaName}
                    </span>
                  </td>
                  <td className="p-2 text-end whitespace-nowrap">
                    {formatDzd(order.total, locale)}
                  </td>
                  <td className="p-2">
                    <OrderStatusBadge status={order.status} t={t} />
                    {order.requiresAttention && (
                      <span className="text-accent ms-2 font-bold">⚠</span>
                    )}
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      )}
      <Pagination page={data.page} totalPages={data.totalPages ?? 0} hrefFor={href} t={t} />
    </>
  );
}
