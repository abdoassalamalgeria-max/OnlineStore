import type { Locale } from "@/i18n/config";
import type { Dictionary } from "@/i18n/messages";
import { removeCover, uploadCover } from "@/lib/actions/admin";
import { BookCover } from "../book-cover";
import { AdminForm } from "./admin-form";
import { formMessages } from "./admin-messages";

type Props = {
  locale: Locale;
  t: Dictionary;
  book: {
    id: string;
    slug: string;
    title: string;
    language: string;
    coverImageUrl?: string | null;
  };
};

/** Shows the current cover and lets the admin replace or remove it. */
export function CoverUploader({ locale, t, book }: Props) {
  return (
    <section
      aria-labelledby="cover"
      className="border-border bg-surface flex flex-col gap-4 rounded-lg border p-4"
    >
      <h2 id="cover" className="font-bold">
        {t.admin.cover}
      </h2>
      <div className="relative aspect-[2/3] w-40">
        <BookCover
          title={book.title}
          url={book.coverImageUrl}
          language={book.language}
          sizes="10rem"
        />
      </div>
      <AdminForm
        action={uploadCover}
        submitLabel={t.admin.uploadCover}
        pendingLabel={t.loading}
        messages={formMessages(t)}
      >
        <input type="hidden" name="locale" value={locale} />
        <input type="hidden" name="id" value={book.id} />
        <input type="hidden" name="slug" value={book.slug} />
        <div>
          <label htmlFor="cover-file" className="sr-only">
            {t.admin.uploadCover}
          </label>
          <input
            id="cover-file"
            name="file"
            type="file"
            required
            accept="image/jpeg,image/png,image/webp"
            aria-describedby="cover-hint"
            className="text-sm"
          />
          <p id="cover-hint" className="text-muted mt-1 text-xs">
            {t.admin.coverHint}
          </p>
        </div>
      </AdminForm>
      {book.coverImageUrl && (
        <form action={removeCover}>
          <input type="hidden" name="locale" value={locale} />
          <input type="hidden" name="id" value={book.id} />
          <input type="hidden" name="slug" value={book.slug} />
          <button type="submit" className="text-muted hover:text-accent text-sm underline">
            {t.admin.removeCover}
          </button>
        </form>
      )}
    </section>
  );
}
