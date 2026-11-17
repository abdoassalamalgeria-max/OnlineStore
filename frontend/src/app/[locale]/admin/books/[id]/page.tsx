import Link from "next/link";
import { notFound } from "next/navigation";
import { Suspense } from "react";
import { BookForm } from "@/components/admin/book-form";
import { CoverUploader } from "@/components/admin/cover-uploader";
import { getDictionary, getLocale } from "@/i18n/dictionaries";
import { getAuthors, getCategories } from "@/lib/api/catalog";
import { ApiError } from "@/lib/api/client";
import { userApi } from "@/lib/api/server-client";

const guid = /^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/i;

export default async function EditBookPage({ params }: PageProps<"/[locale]/admin/books/[id]">) {
  const t = await getDictionary();
  return (
    <div className="flex flex-col gap-6">
      <h1 className="text-3xl font-bold">{t.admin.editBook}</h1>
      <Suspense fallback={<p className="text-muted">{t.loading}</p>}>
        <Editor params={params} />
      </Suspense>
    </div>
  );
}

async function Editor({ params }: Pick<PageProps<"/[locale]/admin/books/[id]">, "params">) {
  const t = await getDictionary();
  const locale = await getLocale();
  const { id } = await params;
  if (!guid.test(id)) notFound();

  const api = await userApi();
  const [{ data: book, response }, authors, categories] = await Promise.all([
    api.GET("/api/v1/admin/books/{id}", { params: { path: { id } } }),
    getAuthors(),
    getCategories(),
  ]);
  if (response.status === 404) notFound();
  if (!book) throw new ApiError(response.status, "/api/v1/admin/books");

  return (
    <div className="grid gap-8 lg:grid-cols-[1fr_16rem]">
      <BookForm locale={locale} t={t} book={book} authors={authors} categories={categories} />
      <div className="flex flex-col gap-4">
        <CoverUploader locale={locale} t={t} book={book} />
        <Link href={`/${locale}/books/${book.slug}`} className="text-brand text-sm underline">
          ↗ /{locale}/books/{book.slug}
        </Link>
        {book.hasOrders && <p className="text-muted text-xs">{t.admin.deleteBlocked}</p>}
      </div>
    </div>
  );
}
