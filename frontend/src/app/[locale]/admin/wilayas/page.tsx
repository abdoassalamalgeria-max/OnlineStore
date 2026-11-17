import { connection } from "next/server";
import { Suspense } from "react";
import { AdminForm } from "@/components/admin/admin-form";
import { formMessages, inputClass } from "@/components/admin/admin-messages";
import { getDictionary, getLocale } from "@/i18n/dictionaries";
import { updateWilaya } from "@/lib/actions/admin";
import { ApiError, api } from "@/lib/api/client";

export default async function AdminWilayasPage() {
  const t = await getDictionary();
  return (
    <div className="flex flex-col gap-6">
      <h1 className="text-3xl font-bold">{t.admin.nav.wilayas}</h1>
      <Suspense fallback={<p className="text-muted">{t.loading}</p>}>
        <Wilayas />
      </Suspense>
    </div>
  );
}

async function Wilayas() {
  const t = await getDictionary();
  const locale = await getLocale();
  // Admin data is per request (never prerendered at build time, when the API may be down).
  await connection();
  // Not cached: the admin must see the values just saved.
  const { data, response } = await api.GET("/api/v1/wilayas", { cache: "no-store" });
  if (!data) throw new ApiError(response.status, "/api/v1/wilayas");
  const f = t.admin.fields;

  return (
    <ul className="grid gap-3 md:grid-cols-2">
      {data.map((w) => (
        <li key={w.code} className="border-border bg-surface rounded-lg border p-3">
          <AdminForm
            action={updateWilaya}
            submitLabel={t.admin.save}
            pendingLabel={t.loading}
            messages={formMessages(t)}
            className="flex flex-wrap items-end gap-3"
          >
            <input type="hidden" name="locale" value={locale} />
            <input type="hidden" name="code" value={w.code} />
            <p className="w-full font-medium">
              {String(w.code).padStart(2, "0")} - {locale === "ar" ? w.nameAr : w.nameFr}
            </p>
            <div className="w-36">
              <label htmlFor={`fee-${w.code}`} className="mb-1 block text-xs">
                {f.fee}
              </label>
              <input
                id={`fee-${w.code}`}
                name="shippingFee"
                type="number"
                min={0}
                step={50}
                defaultValue={w.shippingFee}
                className={inputClass}
              />
            </div>
            <label className="flex items-center gap-2 text-sm">
              <input
                type="checkbox"
                name="isDeliveryAvailable"
                defaultChecked={w.isDeliveryAvailable}
              />
              {f.deliveryAvailable}
            </label>
          </AdminForm>
        </li>
      ))}
    </ul>
  );
}
