import type { Metadata } from "next";
import Link from "next/link";
import { notFound } from "next/navigation";
import { Suspense } from "react";
import { OrderStatusBadge } from "@/components/order-status-badge";
import { intlLocale } from "@/i18n/config";
import { format, getDictionary, getLocale } from "@/i18n/dictionaries";
import { ApiError } from "@/lib/api/client";
import { userApi } from "@/lib/api/server-client";
import { requireSignedIn } from "@/lib/auth/require-user";
import { formatDzd } from "@/lib/format";

const guid = /^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/i;

export async function generateMetadata(): Promise<Metadata> {
  const t = await getDictionary();
  return { title: t.account.orders, robots: { index: false } };
}

export default async function OrderPage({ params }: PageProps<"/[locale]/account/orders/[id]">) {
  const t = await getDictionary();
  const locale = await getLocale();
  return (
    <div className="flex flex-col gap-6">
      <Link href={`/${locale}/account/orders`} className="text-brand text-sm hover:underline">
        {locale === "ar" ? "→" : "←"} {t.account.backToOrders}
      </Link>
      <Suspense fallback={<p className="text-muted">{t.loading}</p>}>
        <OrderDetails params={params} />
      </Suspense>
    </div>
  );
}

async function OrderDetails({
  params,
}: Pick<PageProps<"/[locale]/account/orders/[id]">, "params">) {
  const t = await getDictionary();
  const locale = await getLocale();
  const { id } = await params;
  if (!guid.test(id)) notFound();
  await requireSignedIn(locale, `/${locale}/account/orders/${id}`);

  const api = await userApi();
  const { data: order, response } = await api.GET("/api/v1/orders/{id}", {
    params: { path: { id } },
  });
  if (response.status === 404) notFound();
  if (!order) throw new ApiError(response.status, "/api/v1/orders");

  const a = t.account;
  const dates = new Intl.DateTimeFormat(intlLocale[locale], {
    dateStyle: "medium",
    timeStyle: "short",
  });
  const address = order.shippingAddress;

  return (
    <article className="flex flex-col gap-6">
      <header className="flex flex-wrap items-center gap-3">
        <h1 className="text-2xl font-bold">
          {format(a.orderTitle, { number: "" })}
          <span dir="ltr" className="font-mono">
            {order.orderNumber}
          </span>
        </h1>
        <OrderStatusBadge status={order.status} t={t} />
      </header>

      <dl className="grid grid-cols-[auto_1fr] gap-x-6 gap-y-2 text-sm">
        <dt className="text-muted">{a.placedOn}</dt>
        <dd>
          <time dateTime={order.placedAt}>{dates.format(new Date(order.placedAt))}</time>
        </dd>
        {order.paidAt && (
          <>
            <dt className="text-muted">{a.paidOn}</dt>
            <dd>
              <time dateTime={order.paidAt}>{dates.format(new Date(order.paidAt))}</time>
            </dd>
          </>
        )}
        <dt className="text-muted">{t.order.deliverTo}</dt>
        <dd>
          {address.fullName} · <span dir="ltr">{address.phone}</span>
          <br />
          {[address.addressLine, address.commune, address.wilayaName].join(
            locale === "ar" ? "، " : ", ",
          )}
        </dd>
      </dl>

      <div className="overflow-x-auto">
        <table className="border-border w-full min-w-[28rem] border text-sm">
          <thead className="bg-surface">
            <tr>
              <th scope="col" className="p-2 text-start">
                {t.order.items}
              </th>
              <th scope="col" className="p-2 text-end">
                {a.unitPrice}
              </th>
              <th scope="col" className="p-2 text-end">
                {a.quantity}
              </th>
              <th scope="col" className="p-2 text-end">
                {a.total}
              </th>
            </tr>
          </thead>
          <tbody>
            {order.items.map((item) => (
              <tr key={item.bookId} className="border-border border-t">
                <td className="p-2" dir="auto">
                  {item.title}
                </td>
                <td className="p-2 text-end">{formatDzd(item.unitPrice, locale)}</td>
                <td className="p-2 text-end">{item.quantity}</td>
                <td className="p-2 text-end">{formatDzd(item.lineTotal, locale)}</td>
              </tr>
            ))}
          </tbody>
          <tfoot>
            <tr className="border-border border-t">
              <th scope="row" colSpan={3} className="p-2 text-end font-normal">
                {a.subtotal}
              </th>
              <td className="p-2 text-end">{formatDzd(order.subtotal, locale)}</td>
            </tr>
            <tr>
              <th scope="row" colSpan={3} className="p-2 text-end font-normal">
                {a.shipping}
              </th>
              <td className="p-2 text-end">{formatDzd(order.shippingFee, locale)}</td>
            </tr>
            <tr className="font-bold">
              <th scope="row" colSpan={3} className="p-2 text-end">
                {a.total}
              </th>
              <td className="p-2 text-end">{formatDzd(order.total, locale)}</td>
            </tr>
          </tfoot>
        </table>
      </div>

      {order.paymentUrl && (
        <a
          href={order.paymentUrl}
          className="bg-brand hover:bg-brand-strong w-fit rounded-md px-5 py-2.5 font-medium text-white"
        >
          {t.order.retryPayment}
        </a>
      )}
    </article>
  );
}
