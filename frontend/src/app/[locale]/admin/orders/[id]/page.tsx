import Link from "next/link";
import { notFound } from "next/navigation";
import { Suspense } from "react";
import { AdminForm } from "@/components/admin/admin-form";
import { formMessages, inputClass } from "@/components/admin/admin-messages";
import { OrderStatusBadge } from "@/components/order-status-badge";
import { intlLocale } from "@/i18n/config";
import { getDictionary, getLocale } from "@/i18n/dictionaries";
import { orderAction } from "@/lib/actions/admin";
import { ApiError } from "@/lib/api/client";
import { userApi } from "@/lib/api/server-client";
import { formatDzd } from "@/lib/format";

const guid = /^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/i;

export default async function AdminOrderPage({ params }: PageProps<"/[locale]/admin/orders/[id]">) {
  const t = await getDictionary();
  const locale = await getLocale();
  return (
    <div className="flex flex-col gap-6">
      <Link href={`/${locale}/admin/orders`} className="text-brand text-sm hover:underline">
        {locale === "ar" ? "→" : "←"} {t.admin.nav.orders}
      </Link>
      <Suspense fallback={<p className="text-muted">{t.loading}</p>}>
        <OrderAdmin params={params} />
      </Suspense>
    </div>
  );
}

async function OrderAdmin({ params }: Pick<PageProps<"/[locale]/admin/orders/[id]">, "params">) {
  const t = await getDictionary();
  const locale = await getLocale();
  const { id } = await params;
  if (!guid.test(id)) notFound();

  const api = await userApi();
  const { data: order, response } = await api.GET("/api/v1/admin/orders/{id}", {
    params: { path: { id } },
  });
  if (response.status === 404) notFound();
  if (!order) throw new ApiError(response.status, "/api/v1/admin/orders");

  const o = t.admin.orders;
  const dates = new Intl.DateTimeFormat(intlLocale[locale], {
    dateStyle: "medium",
    timeStyle: "short",
  });
  const when = (iso?: string | null) => (iso ? dates.format(new Date(iso)) : "—");
  const a = order.shippingAddress;

  const actions: {
    action: "Ship" | "Deliver" | "ResolveAttention" | "Cancel";
    label: string;
    show: boolean;
    danger?: boolean;
  }[] = [
    { action: "ResolveAttention", label: o.resolve, show: order.requiresAttention },
    { action: "Ship", label: o.ship, show: order.status === "Paid" && !order.requiresAttention },
    { action: "Deliver", label: o.deliver, show: order.status === "Shipped" },
    {
      action: "Cancel",
      label: o.cancelOrder,
      show: ["Pending", "Paid", "Shipped"].includes(order.status),
      danger: true,
    },
  ];

  return (
    <article className="grid gap-8 lg:grid-cols-[1fr_20rem]">
      <div className="flex flex-col gap-6">
        <header className="flex flex-wrap items-center gap-3">
          <h1 dir="ltr" className="font-mono text-2xl font-bold">
            {order.orderNumber}
          </h1>
          <OrderStatusBadge status={order.status} t={t} />
        </header>

        {order.requiresAttention && (
          <p
            role="alert"
            className="border-accent/40 bg-accent/10 text-accent rounded-md border p-3 text-sm"
          >
            ⚠ {o.attentionText}
          </p>
        )}
        {order.statusNote && <p className="text-muted text-sm">{order.statusNote}</p>}

        <section aria-labelledby="customer">
          <h2 id="customer" className="mb-2 font-bold">
            {o.customer}
          </h2>
          <p>
            {a.fullName} · <span dir="ltr">{a.phone}</span>
          </p>
          <p>
            {[
              a.addressLine,
              a.commune,
              `${String(a.wilayaCode).padStart(2, "0")} ${a.wilayaName}`,
            ].join(", ")}
          </p>
        </section>

        <table className="border-border w-full border text-sm">
          <thead className="bg-surface">
            <tr>
              <th scope="col" className="p-2 text-start">
                {o.items}
              </th>
              <th scope="col" className="p-2 text-end">
                {t.account.quantity}
              </th>
              <th scope="col" className="p-2 text-end">
                {o.total}
              </th>
            </tr>
          </thead>
          <tbody>
            {order.items.map((item) => (
              <tr key={item.bookId} className="border-border border-t">
                <td className="p-2" dir="auto">
                  {item.title}
                </td>
                <td className="p-2 text-end">{item.quantity}</td>
                <td className="p-2 text-end">{formatDzd(item.lineTotal, locale)}</td>
              </tr>
            ))}
          </tbody>
          <tfoot>
            <tr className="border-border border-t">
              <th scope="row" colSpan={2} className="p-2 text-end font-normal">
                {t.account.shipping}
              </th>
              <td className="p-2 text-end">{formatDzd(order.shippingFee, locale)}</td>
            </tr>
            <tr className="font-bold">
              <th scope="row" colSpan={2} className="p-2 text-end">
                {o.total}
              </th>
              <td className="p-2 text-end">{formatDzd(order.total, locale)}</td>
            </tr>
          </tfoot>
        </table>

        <dl className="grid grid-cols-[auto_1fr] gap-x-6 gap-y-1 text-sm">
          <dt className="text-muted">{o.placed}</dt>
          <dd>{when(order.placedAt)}</dd>
          <dt className="text-muted">{o.paid}</dt>
          <dd>{when(order.paidAt)}</dd>
          <dt className="text-muted">{o.shipped}</dt>
          <dd>{when(order.shippedAt)}</dd>
          <dt className="text-muted">{o.delivered}</dt>
          <dd>{when(order.deliveredAt)}</dd>
          <dt className="text-muted">{o.stockReserved}</dt>
          <dd>{order.stockReserved ? "✓" : "✗"}</dd>
          <dt className="text-muted">{o.checkoutId}</dt>
          <dd dir="ltr" className="font-mono text-xs break-all">
            {order.paymentCheckoutId ?? "—"}
          </dd>
        </dl>
      </div>

      <aside className="flex flex-col gap-4">
        {actions
          .filter((x) => x.show)
          .map((x) => (
            <section key={x.action} className="border-border bg-surface rounded-lg border p-4">
              <AdminForm
                action={orderAction}
                submitLabel={x.label}
                pendingLabel={t.loading}
                messages={formMessages(t)}
                variant={x.danger ? "danger" : "primary"}
                confirm={x.danger ? t.admin.confirmDelete : undefined}
              >
                <input type="hidden" name="locale" value={locale} />
                <input type="hidden" name="id" value={order.id} />
                <input type="hidden" name="action" value={x.action} />
                {x.action === "Cancel" && (
                  <>
                    {order.status !== "Pending" && (
                      <p className="text-accent text-xs">{o.cancelPaidWarning}</p>
                    )}
                    <label htmlFor="cancel-note" className="sr-only">
                      {o.note}
                    </label>
                    <textarea
                      id="cancel-note"
                      name="note"
                      rows={2}
                      placeholder={o.notePlaceholder}
                      className={inputClass}
                    />
                  </>
                )}
              </AdminForm>
            </section>
          ))}
      </aside>
    </article>
  );
}
