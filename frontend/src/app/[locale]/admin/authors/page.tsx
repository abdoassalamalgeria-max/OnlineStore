import { connection } from "next/server";
import { Suspense } from "react";
import { AdminForm } from "@/components/admin/admin-form";
import { formMessages, inputClass, labelClass } from "@/components/admin/admin-messages";
import { format, getDictionary, getLocale } from "@/i18n/dictionaries";
import type { Dictionary } from "@/i18n/messages";
import { deleteAuthor, saveAuthor } from "@/lib/actions/admin";
import { getAuthors } from "@/lib/api/catalog";
import type { Schemas } from "@/lib/api/client";

export default async function AdminAuthorsPage() {
  const t = await getDictionary();
  return (
    <div className="flex flex-col gap-6">
      <h1 className="text-3xl font-bold">{t.admin.nav.authors}</h1>
      <Suspense fallback={<p className="text-muted">{t.loading}</p>}>
        <Authors />
      </Suspense>
    </div>
  );
}

function AuthorFields({ t, author }: { t: Dictionary; author?: Schemas["AuthorDto"] }) {
  const f = t.admin.fields;
  const p = author?.id ?? "new";
  return (
    <div className="grid gap-3 sm:grid-cols-2">
      <div>
        <label htmlFor={`name-${p}`} className={labelClass}>
          {f.name}
        </label>
        <input
          id={`name-${p}`}
          name="name"
          required
          defaultValue={author?.name}
          dir="auto"
          className={inputClass}
        />
      </div>
      <div>
        <label htmlFor={`slug-${p}`} className={labelClass}>
          {f.slug}
        </label>
        <input
          id={`slug-${p}`}
          name="slug"
          required
          defaultValue={author?.slug}
          dir="ltr"
          className={inputClass}
        />
      </div>
      <div className="sm:col-span-2">
        <label htmlFor={`bio-${p}`} className={labelClass}>
          {f.bio}
        </label>
        <textarea
          id={`bio-${p}`}
          name="bio"
          rows={2}
          defaultValue={author?.bio ?? ""}
          dir="auto"
          className={inputClass}
        />
      </div>
    </div>
  );
}

async function Authors() {
  const t = await getDictionary();
  const locale = await getLocale();
  // Admin data is per request (never prerendered at build time, when the API may be down).
  await connection();
  const authors = await getAuthors();

  return (
    <div className="flex flex-col gap-6">
      <details className="border-border bg-surface rounded-lg border p-4">
        <summary className="cursor-pointer font-bold">+ {t.admin.add}</summary>
        <div className="mt-4">
          <AdminForm
            action={saveAuthor}
            submitLabel={t.admin.add}
            pendingLabel={t.loading}
            messages={formMessages(t)}
          >
            <input type="hidden" name="locale" value={locale} />
            <AuthorFields t={t} />
          </AdminForm>
        </div>
      </details>

      <ul className="divide-border border-border bg-surface divide-y rounded-lg border">
        {authors.map((author) => (
          <li key={author.id} className="p-4">
            <details>
              <summary className="flex cursor-pointer flex-wrap items-center justify-between gap-2">
                <span dir="auto" className="font-medium">
                  {author.name}
                </span>
                <span className="text-muted text-sm">
                  {author.bookCount} {t.admin.books} · {t.admin.edit}
                </span>
              </summary>
              <div className="mt-4 flex flex-col gap-4">
                <AdminForm
                  action={saveAuthor}
                  submitLabel={t.admin.save}
                  pendingLabel={t.loading}
                  messages={formMessages(t)}
                >
                  <input type="hidden" name="locale" value={locale} />
                  <input type="hidden" name="id" value={author.id} />
                  <AuthorFields t={t} author={author} />
                </AdminForm>
                {author.bookCount === 0 && (
                  <AdminForm
                    action={deleteAuthor}
                    submitLabel={t.admin.delete}
                    pendingLabel={t.loading}
                    messages={formMessages(t)}
                    confirm={format(t.admin.confirmDelete, {})}
                    variant="danger"
                  >
                    <input type="hidden" name="locale" value={locale} />
                    <input type="hidden" name="id" value={author.id} />
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
