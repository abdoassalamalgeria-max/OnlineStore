import type { BookFilters } from "./api/catalog";
import type { BookSort } from "./api/client";

const sorts: readonly BookSort[] = ["Newest", "PriceAsc", "PriceDesc", "Title"];
const bookLanguages = ["ar", "fr", "en", "ber"] as const;

export const catalogPageSize = 12;

type RawParams = Record<string, string | string[] | undefined>;

const first = (value: string | string[] | undefined) =>
  (Array.isArray(value) ? value[0] : value)?.trim() || undefined;

const positiveInt = (value: string | undefined) => {
  const n = Number(value);
  return Number.isInteger(n) && n >= 0 ? n : undefined;
};

/**
 * Turns the catalog URL's query string into API filters. Invalid values are dropped instead of
 * erroring, so a hand-edited URL still shows results.
 */
export function parseCatalogParams(raw: RawParams): BookFilters {
  const sort = first(raw.sort) as BookSort | undefined;
  const language = first(raw.language);
  const page = positiveInt(first(raw.page));

  return {
    search: first(raw.q)?.slice(0, 100),
    category: first(raw.category),
    author: first(raw.author),
    language: bookLanguages.includes(language as (typeof bookLanguages)[number])
      ? language
      : undefined,
    minPrice: positiveInt(first(raw.minPrice)),
    maxPrice: positiveInt(first(raw.maxPrice)),
    inStock: first(raw.inStock) === "1" ? true : undefined,
    sort: sort && sorts.includes(sort) ? sort : undefined,
    page: page && page >= 1 ? page : 1,
    pageSize: catalogPageSize,
  };
}

/** Builds a catalog URL from filters, keeping only non-empty values (used by pagination links). */
export function catalogHref(
  locale: string,
  filters: BookFilters,
  overrides: Partial<BookFilters> = {},
) {
  const merged = { ...filters, ...overrides };
  const query = new URLSearchParams();
  if (merged.search) query.set("q", merged.search);
  if (merged.category) query.set("category", merged.category);
  if (merged.author) query.set("author", merged.author);
  if (merged.language) query.set("language", merged.language);
  if (merged.minPrice !== undefined) query.set("minPrice", String(merged.minPrice));
  if (merged.maxPrice !== undefined) query.set("maxPrice", String(merged.maxPrice));
  if (merged.inStock) query.set("inStock", "1");
  if (merged.sort && merged.sort !== "Newest") query.set("sort", merged.sort);
  if (merged.page && merged.page > 1) query.set("page", String(merged.page));
  const qs = query.toString();
  return `/${locale}/books${qs ? `?${qs}` : ""}`;
}

export const sortOptions = sorts;
export const languageOptions = bookLanguages;
