import Link from "next/link";
import { notFound, redirect } from "next/navigation";
import type { Locale } from "@/i18n/config";
import { format, type Dictionary } from "@/i18n/messages";
import { ApiError } from "@/lib/api/client";
import { userApi } from "@/lib/api/server-client";
import { getAccessToken } from "@/lib/auth/session";
import { formatDzd } from "@/lib/format";
import { AutoRefresh } from "./form-bits";

const guid = /^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/i;

/**
 * Shown when Chargily sends the customer back (success_url / failure_url). The redirect itself
 * proves nothing, so the page displays the order status the API knows, which only the webhook
 * changes. While it is still Pending the page refreshes itself every few seconds.
 */
export async function OrderResult({
  orderId,
  locale,
  t,
  returnedFrom,
}: {
  orderId: string | undefined;
  locale: Locale;
  t: Dictionary;
  returnedFrom: "success" | "failure";
}) {
  if (!orderId || !guid.test(orderId)) notFound();
  if (!(await getAccessToken())) {
    redirect(
      `/${locale}/login?returnTo=${encodeURIComponent(`/${locale}/checkout/${returnedFrom}?order=${orderId}`)}`,
    );
  }

  const api = await userApi();
  const { data: order, response } = await api.GET("/api/v1/orders/{id}", {
    params: { path: { id: orderId } },
  });
  if (response.status === 404) notFound();
  if (!order) throw new ApiError(response.status, "/api/v1/orders");

  const o = t.order;
  const paid =
    order.status === "Paid" || order.status === "Shipped" || order.status === "Delivered";
  const pending = order.status === "Pending";

  return (
    <section className="mx-auto flex w-full max-w-2xl flex-col gap-6">
      <div
        role="status"
        aria-live="polite"
        className={`rounded-lg border p-6 ${paid ? "border-brand/40 bg-brand/10" : pending ? "border-border bg-surface" : "border-accent/40 bg-accent/10"}`}
      >
        <h1 className="text-2xl font-bold">
          {paid ? o.successTitle : pending ? o.pendingTitle : o.failureTitle}
        </h1>
        <p className="mt-2">
          {paid
            ? format(o.successText, { phone: order.shippingAddress.phone })
            : pending
              ? o.pendingText
              : o.failureText}
        </p>
        {pending && returnedFrom === "success" && (
          <p className="text-muted mt-2 text-sm">{o.stillPending}</p>
        )}
        {pending && <AutoRefresh seconds={3} maxTimes={40} />}
      </div>

      <dl className="grid grid-cols-[auto_1fr] gap-x-6 gap-y-2 text-sm">
        <dt className="text-muted">{o.number}</dt>
        <dd dir="ltr" className="text-start font-mono">
          {order.orderNumber}
        </dd>
        <dt className="text-muted">{t.checkout.total}</dt>
        <dd className="font-bold">{formatDzd(order.total, locale)}</dd>
        <dt className="text-muted">{o.deliverTo}</dt>
        <dd>
          {[
            order.shippingAddress.fullName,
            order.shippingAddress.commune,
            order.shippingAddress.wilayaName,
          ].join(locale === "ar" ? "، " : ", ")}
        </dd>
        <dt className="text-muted">{o.items}</dt>
        <dd>
          <ul>
            {order.items.map((item) => (
              <li key={item.bookId}>
                {item.title} × {item.quantity}
              </li>
            ))}
          </ul>
        </dd>
      </dl>

      <div className="flex flex-wrap gap-3">
        {pending && order.paymentUrl && (
          <a
            href={order.paymentUrl}
            className="bg-brand hover:bg-brand-strong rounded-md px-5 py-2.5 font-medium text-white"
          >
            {o.retryPayment}
          </a>
        )}
        {!paid && (
          <Link
            href={`/${locale}/cart`}
            className="border-border rounded-md border px-5 py-2.5 font-medium"
          >
            {o.backToCart}
          </Link>
        )}
        {paid && (
          <Link
            href={`/${locale}/books`}
            className="border-border rounded-md border px-5 py-2.5 font-medium"
          >
            {t.cart.continueShopping}
          </Link>
        )}
      </div>
    </section>
  );
}
