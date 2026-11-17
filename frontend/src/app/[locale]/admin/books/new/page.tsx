import { connection } from "next/server";
import { Suspense } from "react";
import { BookForm } from "@/components/admin/book-form";
import { getDictionary, getLocale } from "@/i18n/dictionaries";
import { getAuthors, getCategories } from "@/lib/api/catalog";

export default async function NewBookPage() {
  const t = await getDictionary();
  return (
    <div className="flex flex-col gap-6">
      <h1 className="text-3xl font-bold">{t.admin.newBook}</h1>
      <p className="text-muted text-sm">{t.admin.coverAfterCreate}</p>
      <Suspense fallback={<p className="text-muted">{t.loading}</p>}>
        <Form />
      </Suspense>
    </div>
  );
}

async function Form() {
  const t = await getDictionary();
  const locale = await getLocale();
  // Admin data is per request (never prerendered at build time, when the API may be down).
  await connection();
  const [authors, categories] = await Promise.all([getAuthors(), getCategories()]);
  return <BookForm locale={locale} t={t} book={null} authors={authors} categories={categories} />;
}
