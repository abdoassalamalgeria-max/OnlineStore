import Link from "next/link";
import { Suspense } from "react";
import { BookCover } from "@/components/book-cover";
import { Pagination } from "@/components/pagination";
import { getDictionary, getLocale } from "@/i18n/dictionaries";
import { ApiError } from "@/lib/api/client";
import { userApi } from "@/lib/api/server-client";
import { formatDzd } from "@/lib/format";
import { inputClass } from "@/components/admin/admin-messages";

export default async function AdminBooksPage({ searchParams }: PageProps<"/[locale]/admin/books">) {
  const t = await getDictionary();
  const locale = await getLocale();
  return (
    <div className="flex flex-col gap-6">
      <div className="flex flex-wrap items-center justify-between gap-3">
        <h1 className="text-3xl font-bold">{t.admin.nav.books}</h1>
        <Link
          href={`/${locale}/admin/books/new`}
          className="bg-brand hover:bg-brand-strong rounded-md px-4 py-2 text-sm font-medium text-white"
        >
          + {t.admin.newBook}
        </Link>
      </div>
      <Suspense fallback={<p className="text-muted">{t.loading}</p>}>
        <BooksTable searchParams={searchParams} />
      </Suspense>
    </div>
  );
}

async function BooksTable({
  searchParams,
}: Pick<PageProps<"/[locale]/admin/books">, "searchParams">) {
  const t = await getDictionary();
  const locale = await getLocale();
  const params = await searchParams;
  const search = typeof params.q === "string" ? params.q : "";
  const lowStock = params.lowStock === "1";
  const page = Math.max(1, Number(params.page) || 1);

  const api = await userApi();
  const { data, response } = await api.GET("/api/v1/admin/books", {
    params: {
      query: { search: search || undefined, lowStock: lowStock || undefined, page, pageSize: 20 },
    },
  });
  if (!data) throw new ApiError(response.status, "/api/v1/admin/books");

  const href = (p: number) => {
    const q = new URLSearchParams();
    if (search) q.set("q", search);
    if (lowStock) q.set("lowStock", "1");
    if (p > 1) q.set("page", String(p));
    return `/${locale}/admin/books${q.size ? `?${q}` : ""}`;
  };

  return (
    <>
      <form method="get" className="flex flex-wrap items-center gap-3">
        <label htmlFor="admin-book-search" className="sr-only">
          {t.admin.searchBooks}
        </label>
        <input
          id="admin-book-search"
          name="q"
          defaultValue={search}
          placeholder={t.admin.searchBooks}
          className={`${inputClass} max-w-xs`}
        />
        <label className="flex items-center gap-2 text-sm">
          <input type="checkbox" name="lowStock" value="1" defaultChecked={lowStock} />
          {t.admin.lowStockOnly}
        </label>
        <button type="submit" className="border-border rounded-md border px-4 py-2 text-sm">
          {t.admin.search}
        </button>
      </form>

      {data.items.length === 0 ? (
        <p className="text-muted">{t.admin.noItems}</p>
      ) : (
        <div className="overflow-x-auto">
          <table className="w-full min-w-[40rem] text-sm">
            <thead className="text-muted border-border border-b text-start">
              <tr>
                <th scope="col" className="p-2 text-start">
                  {t.admin.fields.title}
                </th>
                <th scope="col" className="p-2 text-end">
                  {t.admin.price}
                </th>
                <th scope="col" className="p-2 text-end">
                  {t.admin.stock}
                </th>
                <th scope="col" className="p-2">
                  <span className="sr-only">{t.admin.edit}</span>
                </th>
              </tr>
            </thead>
            <tbody>
              {data.items.map((book) => (
                <tr key={book.id} className="border-border border-b">
                  <td className="p-2">
                    <div className="flex items-center gap-3">
                      <div className="relative aspect-[2/3] w-10 shrink-0">
                        <BookCover
                          title={book.title}
                          url={book.coverImageUrl}
                          language={book.language}
                          sizes="2.5rem"
                        />
                      </div>
                      <div>
                        <p dir="auto" className="font-medium">
                          {book.title}{" "}
                          {book.isFeatured && <span title={t.admin.fields.featured}>★</span>}
                        </p>
                        <p className="text-muted text-xs">{book.authors.join(", ")}</p>
                      </div>
                    </div>
                  </td>
                  <td className="p-2 text-end whitespace-nowrap">
                    {formatDzd(book.salePrice ?? book.price, locale)}
                    {book.salePrice !== null && (
                      <del className="text-muted block text-xs">
                        {formatDzd(book.price, locale)}
                      </del>
                    )}
                  </td>
                  <td
                    className={`p-2 text-end font-medium ${book.stock <= 5 ? "text-accent" : ""}`}
                  >
                    {book.stock}
                  </td>
                  <td className="p-2 text-end">
                    <Link
                      href={`/${locale}/admin/books/${book.id}`}
                      className="text-brand underline"
                    >
                      {t.admin.edit}
                    </Link>
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
