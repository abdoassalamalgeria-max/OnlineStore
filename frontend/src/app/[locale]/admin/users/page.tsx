import Link from "next/link";
import { Suspense } from "react";
import { inputClass } from "@/components/admin/admin-messages";
import { UserStatusBadge } from "@/components/admin/user-status-badge";
import { Pagination } from "@/components/pagination";
import { intlLocale } from "@/i18n/config";
import { getDictionary, getLocale } from "@/i18n/dictionaries";
import { ApiError } from "@/lib/api/client";
import { adminApi } from "@/lib/api/server-client";

export default async function AdminUsersPage({ searchParams }: PageProps<"/[locale]/admin/users">) {
  const t = await getDictionary();
  return (
    <div className="flex flex-col gap-6">
      <h1 className="text-3xl font-bold">{t.admin.nav.users}</h1>
      <Suspense fallback={<p className="text-muted">{t.loading}</p>}>
        <Users searchParams={searchParams} />
      </Suspense>
    </div>
  );
}

async function Users({ searchParams }: Pick<PageProps<"/[locale]/admin/users">, "searchParams">) {
  const t = await getDictionary();
  const locale = await getLocale();
  const params = await searchParams;
  const search = typeof params.q === "string" ? params.q : "";
  const state = params.state === "suspended" || params.state === "active" ? params.state : "";
  const page = Math.max(1, Number(params.page) || 1);

  const api = await adminApi();
  const { data, response } = await api.GET("/api/v1/admin/users", {
    params: {
      query: {
        search: search || undefined,
        suspended: state === "" ? undefined : state === "suspended",
        page,
        pageSize: 20,
      },
    },
  });
  if (!data) throw new ApiError(response.status, "/api/v1/admin/users");

  const u = t.admin.users;
  const dates = new Intl.DateTimeFormat(intlLocale[locale], { dateStyle: "medium" });
  const href = (p: number) => {
    const q = new URLSearchParams();
    if (search) q.set("q", search);
    if (state) q.set("state", state);
    if (p > 1) q.set("page", String(p));
    return `/${locale}/admin/users${q.size ? `?${q}` : ""}`;
  };

  return (
    <>
      <form method="get" className="flex flex-wrap items-center gap-3">
        <label htmlFor="user-search" className="sr-only">
          {u.search}
        </label>
        <input
          id="user-search"
          name="q"
          defaultValue={search}
          placeholder={u.search}
          className={`${inputClass} max-w-xs`}
        />
        <label htmlFor="user-state" className="sr-only">
          {u.status}
        </label>
        <select
          id="user-state"
          name="state"
          defaultValue={state}
          className={`${inputClass} w-auto`}
        >
          <option value="">{u.all}</option>
          <option value="active">{u.activeOnly}</option>
          <option value="suspended">{u.suspendedOnly}</option>
        </select>
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
                  {u.name}
                </th>
                <th scope="col" className="p-2 text-start">
                  {u.email}
                </th>
                <th scope="col" className="p-2 text-start">
                  {u.phone}
                </th>
                <th scope="col" className="p-2 text-start">
                  {u.registered}
                </th>
                <th scope="col" className="p-2 text-end">
                  {u.orders}
                </th>
                <th scope="col" className="p-2 text-start">
                  {u.status}
                </th>
              </tr>
            </thead>
            <tbody>
              {data.items.map((user) => (
                <tr key={user.id} className="border-border border-b">
                  <td className="p-2">
                    <Link
                      href={`/${locale}/admin/users/${user.id}`}
                      className="text-brand font-medium underline"
                    >
                      {user.fullName}
                    </Link>
                    {user.roles.includes("Admin") && (
                      <span className="bg-gold/20 text-foreground ms-2 rounded px-1.5 py-0.5 text-xs">
                        {u.roleAdmin}
                      </span>
                    )}
                  </td>
                  <td className="p-2" dir="ltr">
                    {user.email}
                  </td>
                  <td className="p-2" dir="ltr">
                    {user.phoneNumber ?? "—"}
                  </td>
                  <td className="p-2 whitespace-nowrap">
                    {dates.format(new Date(user.createdAt))}
                  </td>
                  <td className="p-2 text-end">{user.orderCount}</td>
                  <td className="p-2">
                    <UserStatusBadge suspended={user.isSuspended} t={t} />
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
