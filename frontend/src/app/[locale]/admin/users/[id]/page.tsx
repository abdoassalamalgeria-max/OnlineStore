import Link from "next/link";
import { notFound } from "next/navigation";
import { Suspense } from "react";
import { AdminForm } from "@/components/admin/admin-form";
import { formMessages, inputClass, labelClass } from "@/components/admin/admin-messages";
import { UserStatusBadge } from "@/components/admin/user-status-badge";
import { OrderStatusBadge } from "@/components/order-status-badge";
import { intlLocale } from "@/i18n/config";
import { getDictionary, getLocale } from "@/i18n/dictionaries";
import { format } from "@/i18n/messages";
import { setUserSuspended, updateUser } from "@/lib/actions/admin";
import { ApiError } from "@/lib/api/client";
import { adminApi } from "@/lib/api/server-client";
import { formatDzd } from "@/lib/format";

export default async function AdminUserPage({ params }: PageProps<"/[locale]/admin/users/[id]">) {
  const t = await getDictionary();
  const locale = await getLocale();
  return (
    <div className="flex flex-col gap-6">
      <Link href={`/${locale}/admin/users`} className="text-brand text-sm hover:underline">
        ← {t.admin.users.back}
      </Link>
      <Suspense fallback={<p className="text-muted">{t.loading}</p>}>
        <UserDetails params={params} />
      </Suspense>
    </div>
  );
}

async function UserDetails({ params }: Pick<PageProps<"/[locale]/admin/users/[id]">, "params">) {
  const t = await getDictionary();
  const locale = await getLocale();
  const { id } = await params;

  const api = await adminApi();
  const { data: user, response } = await api.GET("/api/v1/admin/users/{id}", {
    params: { path: { id } },
  });
  if (response.status === 404 || response.status === 400) notFound();
  if (!user) throw new ApiError(response.status, "/api/v1/admin/users/{id}");

  const u = t.admin.users;
  const dates = new Intl.DateTimeFormat(intlLocale[locale], { dateStyle: "medium" });
  const dateTimes = new Intl.DateTimeFormat(intlLocale[locale], {
    dateStyle: "short",
    timeStyle: "short",
  });
  const isAdmin = user.roles.includes("Admin");

  return (
    <>
      <div className="flex flex-wrap items-center gap-3">
        <h1 className="text-3xl font-bold">{user.fullName}</h1>
        <UserStatusBadge suspended={user.isSuspended} t={t} />
        <span className="bg-surface border-border rounded border px-2 py-0.5 text-xs">
          {isAdmin ? u.roleAdmin : u.roleCustomer}
        </span>
      </div>

      <dl className="grid gap-3 sm:grid-cols-4">
        {[
          [u.registered, dates.format(new Date(user.createdAt))],
          [u.orders, String(user.orderCount)],
          [u.totalSpent, formatDzd(user.totalSpent, locale)],
          [u.email, user.email],
        ].map(([label, value]) => (
          <div key={label} className="border-border bg-surface rounded-lg border p-4">
            <dt className="text-muted text-xs">{label}</dt>
            <dd className="mt-1 font-bold break-all" dir="auto">
              {value}
            </dd>
          </div>
        ))}
      </dl>

      {user.lockedOutUntil && (
        <p role="status" className="text-accent text-sm">
          {format(u.lockedUntil, { date: dateTimes.format(new Date(user.lockedOutUntil)) })}
        </p>
      )}

      <div className="grid gap-6 lg:grid-cols-[2fr_1fr]">
        <section
          aria-labelledby="edit-user"
          className="border-border bg-surface rounded-lg border p-5"
        >
          <h2 id="edit-user" className="mb-4 text-lg font-bold">
            {u.editTitle}
          </h2>
          <AdminForm
            action={updateUser}
            submitLabel={t.admin.save}
            pendingLabel={t.loading}
            messages={formMessages(t)}
          >
            <input type="hidden" name="locale" value={locale} />
            <input type="hidden" name="id" value={user.id} />
            <div>
              <label htmlFor="fullName" className={labelClass}>
                {u.name}
              </label>
              <input
                id="fullName"
                name="fullName"
                required
                maxLength={100}
                defaultValue={user.fullName}
                className={inputClass}
              />
            </div>
            <div>
              <label htmlFor="email" className={labelClass}>
                {u.email}
              </label>
              <input
                id="email"
                name="email"
                type="email"
                required
                dir="ltr"
                defaultValue={user.email}
                aria-describedby="email-hint"
                className={inputClass}
              />
              <p id="email-hint" className="text-muted mt-1 text-xs">
                {u.emailHint}
              </p>
            </div>
            <div>
              <label htmlFor="phoneNumber" className={labelClass}>
                {u.phone}
              </label>
              <input
                id="phoneNumber"
                name="phoneNumber"
                type="tel"
                dir="ltr"
                defaultValue={user.phoneNumber ?? ""}
                placeholder="0550123456"
                className={inputClass}
              />
            </div>
          </AdminForm>
        </section>

        <aside className="flex flex-col gap-4">
          <section
            aria-labelledby="account-state"
            className="border-border bg-surface rounded-lg border p-5"
          >
            <h2 id="account-state" className="mb-2 text-lg font-bold">
              {u.account}
            </h2>
            {isAdmin ? (
              <p className="text-muted text-sm">{u.cannotSuspendAdmin}</p>
            ) : (
              <AdminForm
                action={setUserSuspended}
                submitLabel={user.isSuspended ? u.reactivate : u.suspend}
                pendingLabel={t.loading}
                messages={{ ...formMessages(t), blocked: u.cannotSuspendAdmin }}
                variant={user.isSuspended ? "primary" : "danger"}
                confirm={user.isSuspended ? undefined : u.confirmSuspend}
              >
                <input type="hidden" name="locale" value={locale} />
                <input type="hidden" name="id" value={user.id} />
                <input
                  type="hidden"
                  name="action"
                  value={user.isSuspended ? "reactivate" : "suspend"}
                />
                <p className="text-muted text-sm">
                  {user.isSuspended ? u.reactivateHint : u.suspendHint}
                </p>
              </AdminForm>
            )}
          </section>
        </aside>
      </div>

      <section aria-labelledby="user-orders">
        <h2 id="user-orders" className="mb-3 text-lg font-bold">
          {u.recentOrders}
        </h2>
        {user.recentOrders.length === 0 ? (
          <p className="text-muted">{u.noOrders}</p>
        ) : (
          <ul className="flex flex-col gap-2">
            {user.recentOrders.map((order) => (
              <li
                key={order.id}
                className="border-border bg-surface flex flex-wrap items-center gap-3 rounded-lg border p-3 text-sm"
              >
                <Link
                  href={`/${locale}/admin/orders/${order.id}`}
                  dir="ltr"
                  className="text-brand font-mono underline"
                >
                  {order.orderNumber}
                </Link>
                <span>{dateTimes.format(new Date(order.placedAt))}</span>
                <OrderStatusBadge status={order.status} t={t} />
                <span className="ms-auto font-bold">{formatDzd(order.total, locale)}</span>
              </li>
            ))}
          </ul>
        )}
      </section>
    </>
  );
}
