import Link from "next/link";
import { format, type Dictionary } from "@/i18n/dictionaries";

type Props = {
  page: number;
  totalPages: number;
  hrefFor: (page: number) => string;
  t: Dictionary;
};

/** Numbered pagination with previous/next. Shows at most 7 page links around the current one. */
export function Pagination({ page, totalPages, hrefFor, t }: Props) {
  if (totalPages <= 1) {
    return null;
  }

  const start = Math.max(1, Math.min(page - 3, totalPages - 6));
  const pages = Array.from({ length: Math.min(7, totalPages) }, (_, i) => start + i);
  const linkClass =
    "rounded-md border border-border px-3 py-1.5 hover:border-brand hover:text-brand";

  return (
    <nav aria-label={t.pagination.label} className="mt-8 flex justify-center">
      <ul className="flex flex-wrap items-center gap-2 text-sm">
        {page > 1 && (
          <li>
            <Link href={hrefFor(page - 1)} rel="prev" className={linkClass}>
              {t.pagination.previous}
            </Link>
          </li>
        )}
        {pages.map((p) => (
          <li key={p}>
            {p === page ? (
              <span
                aria-current="page"
                className="bg-brand rounded-md px-3 py-1.5 font-bold text-white"
              >
                {p}
              </span>
            ) : (
              <Link
                href={hrefFor(p)}
                aria-label={format(t.pagination.page, { page: p })}
                className={linkClass}
              >
                {p}
              </Link>
            )}
          </li>
        ))}
        {page < totalPages && (
          <li>
            <Link href={hrefFor(page + 1)} rel="next" className={linkClass}>
              {t.pagination.next}
            </Link>
          </li>
        )}
      </ul>
    </nav>
  );
}
