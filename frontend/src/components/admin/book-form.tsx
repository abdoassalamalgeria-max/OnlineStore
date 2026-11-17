import type { Locale } from "@/i18n/config";
import type { Dictionary } from "@/i18n/messages";
import { deleteBook, saveBook } from "@/lib/actions/admin";
import type { Schemas } from "@/lib/api/client";
import { languageOptions } from "@/lib/catalog-params";
import { localizedName } from "@/lib/localize";
import { AdminForm } from "./admin-form";
import { formMessages, inputClass, labelClass } from "./admin-messages";

type Props = {
  locale: Locale;
  t: Dictionary;
  book: Schemas["AdminBookDto"] | null;
  authors: Schemas["AuthorDto"][];
  categories: Schemas["CategoryDto"][];
};

/** Create/edit form for a book (server-rendered fields inside the client AdminForm). */
export function BookForm({ locale, t, book, authors, categories }: Props) {
  const f = t.admin.fields;
  const field = (name: string, label: string, input: React.ReactNode, hint?: string) => (
    <div>
      <label htmlFor={name} className={labelClass}>
        {label}
      </label>
      {input}
      {hint && <p className="text-muted mt-1 text-xs">{hint}</p>}
    </div>
  );

  return (
    <div className="flex flex-col gap-8">
      <AdminForm
        action={saveBook}
        submitLabel={t.admin.save}
        pendingLabel={t.loading}
        messages={formMessages(t)}
        className="grid gap-4 md:grid-cols-2"
      >
        <input type="hidden" name="locale" value={locale} />
        {book && <input type="hidden" name="id" value={book.id} />}
        {book && <input type="hidden" name="previousSlug" value={book.slug} />}

        <div className="md:col-span-2">
          {field(
            "title",
            f.title,
            <input
              id="title"
              name="title"
              required
              defaultValue={book?.title}
              dir="auto"
              className={inputClass}
            />,
          )}
        </div>
        {field(
          "slug",
          f.slug,
          <input
            id="slug"
            name="slug"
            required
            defaultValue={book?.slug}
            dir="ltr"
            pattern="[a-z0-9]+(-[a-z0-9]+)*"
            className={inputClass}
          />,
          f.slugHint,
        )}
        {field(
          "isbn",
          f.isbn,
          <input
            id="isbn"
            name="isbn"
            required
            defaultValue={book?.isbn}
            dir="ltr"
            inputMode="numeric"
            className={inputClass}
          />,
        )}
        <div className="md:col-span-2">
          {field(
            "description",
            f.description,
            <textarea
              id="description"
              name="description"
              required
              rows={4}
              defaultValue={book?.description}
              dir="auto"
              className={inputClass}
            />,
          )}
        </div>
        {field(
          "price",
          `${t.admin.price} (DZD)`,
          <input
            id="price"
            name="price"
            type="number"
            min={1}
            required
            defaultValue={book?.price}
            className={inputClass}
          />,
        )}
        {field(
          "salePrice",
          t.admin.salePrice,
          <input
            id="salePrice"
            name="salePrice"
            type="number"
            min={1}
            defaultValue={book?.salePrice ?? ""}
            className={inputClass}
          />,
        )}
        {field(
          "stock",
          t.admin.stock,
          <input
            id="stock"
            name="stock"
            type="number"
            min={0}
            required
            defaultValue={book?.stock ?? 0}
            className={inputClass}
          />,
        )}
        {field(
          "pageCount",
          f.pageCount,
          <input
            id="pageCount"
            name="pageCount"
            type="number"
            min={1}
            required
            defaultValue={book?.pageCount}
            className={inputClass}
          />,
        )}
        {field(
          "language",
          f.language,
          <select
            id="language"
            name="language"
            defaultValue={book?.language ?? "ar"}
            className={inputClass}
          >
            {languageOptions.map((code) => (
              <option key={code} value={code}>
                {t.bookLanguages[code]}
              </option>
            ))}
          </select>,
        )}
        {field(
          "publishedOn",
          f.publishedOn,
          <input
            id="publishedOn"
            name="publishedOn"
            type="date"
            defaultValue={book?.publishedOn ?? ""}
            className={inputClass}
          />,
        )}
        <div className="md:col-span-2">
          {field(
            "publisher",
            f.publisher,
            <input
              id="publisher"
              name="publisher"
              required
              defaultValue={book?.publisher}
              dir="auto"
              className={inputClass}
            />,
          )}
        </div>

        <fieldset className="border-border rounded-md border p-3">
          <legend className="px-1 text-sm font-medium">{f.authors}</legend>
          <div className="flex max-h-48 flex-col gap-1 overflow-y-auto text-sm">
            {authors.map((a) => (
              <label key={a.id} className="flex items-center gap-2">
                <input
                  type="checkbox"
                  name="authorIds"
                  value={a.id}
                  defaultChecked={book?.authorIds.includes(a.id)}
                />
                <span dir="auto">{a.name}</span>
              </label>
            ))}
          </div>
        </fieldset>
        <fieldset className="border-border rounded-md border p-3">
          <legend className="px-1 text-sm font-medium">{f.categories}</legend>
          <div className="flex max-h-48 flex-col gap-1 overflow-y-auto text-sm">
            {categories.map((c) => (
              <label key={c.id} className="flex items-center gap-2">
                <input
                  type="checkbox"
                  name="categoryIds"
                  value={c.id}
                  defaultChecked={book?.categoryIds.includes(c.id)}
                />
                {localizedName(c, locale)}
              </label>
            ))}
          </div>
        </fieldset>

        <label className="flex items-center gap-2 text-sm md:col-span-2">
          <input type="checkbox" name="isFeatured" defaultChecked={book?.isFeatured} />
          {f.featured}
        </label>
      </AdminForm>

      {book && !book.hasOrders && (
        <AdminForm
          action={deleteBook}
          submitLabel={t.admin.delete}
          pendingLabel={t.loading}
          messages={formMessages(t)}
          confirm={t.admin.confirmDelete}
          variant="danger"
        >
          <input type="hidden" name="locale" value={locale} />
          <input type="hidden" name="id" value={book.id} />
          <input type="hidden" name="slug" value={book.slug} />
        </AdminForm>
      )}
    </div>
  );
}
