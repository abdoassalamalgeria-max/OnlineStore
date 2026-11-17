import { connection } from "next/server";
import { Suspense } from "react";
import { AdminForm } from "@/components/admin/admin-form";
import { formMessages, inputClass, labelClass } from "@/components/admin/admin-messages";
import { getDictionary, getLocale } from "@/i18n/dictionaries";
import type { Dictionary } from "@/i18n/messages";
import { deleteCategory, saveCategory } from "@/lib/actions/admin";
import { getCategories } from "@/lib/api/catalog";
import type { Schemas } from "@/lib/api/client";
import { localizedName } from "@/lib/localize";

export default async function AdminCategoriesPage() {
  const t = await getDictionary();
  return (
    <div className="flex flex-col gap-6">
      <h1 className="text-3xl font-bold">{t.admin.nav.categories}</h1>
      <Suspense fallback={<p className="text-muted">{t.loading}</p>}>
        <Categories />
      </Suspense>
    </div>
  );
}

function CategoryFields({ t, category }: { t: Dictionary; category?: Schemas["CategoryDto"] }) {
  const f = t.admin.fields;
  const p = category?.id ?? "new";
  const input = (
    name: "nameAr" | "nameFr" | "nameEn" | "slug",
    label: string,
    dir: "rtl" | "ltr",
  ) => (
    <div>
      <label htmlFor={`${name}-${p}`} className={labelClass}>
        {label}
      </label>
      <input
        id={`${name}-${p}`}
        name={name}
        required
        defaultValue={category?.[name]}
        dir={dir}
        className={inputClass}
      />
    </div>
  );
  return (
    <div className="grid gap-3 sm:grid-cols-2">
      {input("nameAr", f.nameAr, "rtl")}
      {input("nameFr", f.nameFr, "ltr")}
      {input("nameEn", f.nameEn, "ltr")}
      {input("slug", f.slug, "ltr")}
    </div>
  );
}

async function Categories() {
  const t = await getDictionary();
  const locale = await getLocale();
  // Admin data is per request (never prerendered at build time, when the API may be down).
  await connection();
  const categories = await getCategories();

  return (
    <div className="flex flex-col gap-6">
      <details className="border-border bg-surface rounded-lg border p-4">
        <summary className="cursor-pointer font-bold">+ {t.admin.add}</summary>
        <div className="mt-4">
          <AdminForm
            action={saveCategory}
            submitLabel={t.admin.add}
            pendingLabel={t.loading}
            messages={formMessages(t)}
          >
            <input type="hidden" name="locale" value={locale} />
            <CategoryFields t={t} />
          </AdminForm>
        </div>
      </details>

      <ul className="divide-border border-border bg-surface divide-y rounded-lg border">
        {categories.map((category) => (
          <li key={category.id} className="p-4">
            <details>
              <summary className="flex cursor-pointer flex-wrap items-center justify-between gap-2">
                <span className="font-medium">{localizedName(category, locale)}</span>
                <span className="text-muted text-sm">
                  {category.bookCount} {t.admin.books} · {t.admin.edit}
                </span>
              </summary>
              <div className="mt-4 flex flex-col gap-4">
                <AdminForm
                  action={saveCategory}
                  submitLabel={t.admin.save}
                  pendingLabel={t.loading}
                  messages={formMessages(t)}
                >
                  <input type="hidden" name="locale" value={locale} />
                  <input type="hidden" name="id" value={category.id} />
                  <CategoryFields t={t} category={category} />
                </AdminForm>
                {category.bookCount === 0 && (
                  <AdminForm
                    action={deleteCategory}
                    submitLabel={t.admin.delete}
                    pendingLabel={t.loading}
                    messages={formMessages(t)}
                    confirm={t.admin.confirmDelete}
                    variant="danger"
                  >
                    <input type="hidden" name="locale" value={locale} />
                    <input type="hidden" name="id" value={category.id} />
                  </AdminForm>
                )}
              </div>
            </details>
          </li>
        ))}
      </ul>
    </div>
  );
}
