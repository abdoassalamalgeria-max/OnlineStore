import Link from "next/link";
import type { Locale } from "@/i18n/config";
import type { Dictionary } from "@/i18n/dictionaries";
import type { Author, Category } from "@/lib/api/client";
import type { BookFilters } from "@/lib/api/catalog";
import { languageOptions, sortOptions } from "@/lib/catalog-params";
import { localizedName } from "@/lib/localize";
import { AutoSubmitSelect } from "./auto-submit-select";

type Props = {
  locale: Locale;
  filters: BookFilters;
  categories: Category[];
  authors: Author[];
  t: Dictionary;
};

const fieldClass = "w-full rounded-md border border-border bg-background px-3 py-2 text-sm";
const labelClass = "mb-1 block text-sm font-medium";

/** Server-rendered GET form; changing the sort submits immediately when JavaScript is available. */
export function CatalogFilters({ locale, filters, categories, authors, t }: Props) {
  return (
    <aside>
      <details open className="border-border bg-surface rounded-lg border p-4">
        <summary className="cursor-pointer font-bold">{t.catalog.filters}</summary>

        <form action={`/${locale}/books`} method="get" className="mt-4 flex flex-col gap-4">
          <div>
            <label htmlFor="f-q" className={labelClass}>
              {t.catalog.searchLabel}
            </label>
            <input
              id="f-q"
              name="q"
              type="search"
              defaultValue={filters.search}
              placeholder={t.catalog.searchPlaceholder}
              className={fieldClass}
            />
          </div>

          <div>
            <label htmlFor="f-sort" className={labelClass}>
              {t.catalog.sort}
            </label>
            <AutoSubmitSelect
              id="f-sort"
              name="sort"
              defaultValue={filters.sort ?? "Newest"}
              className={fieldClass}
            >
              {sortOptions.map((sort) => (
                <option key={sort} value={sort}>
                  {t.sort[sort]}
                </option>
              ))}
            </AutoSubmitSelect>
          </div>

          <div>
            <label htmlFor="f-category" className={labelClass}>
              {t.catalog.category}
            </label>
            <select
              id="f-category"
              name="category"
              defaultValue={filters.category ?? ""}
              className={fieldClass}
            >
              <option value="">{t.catalog.any}</option>
              {categories.map((c) => (
                <option key={c.id} value={c.slug}>
                  {localizedName(c, locale)} ({c.bookCount})
                </option>
              ))}
            </select>
          </div>

          <div>
            <label htmlFor="f-author" className={labelClass}>
              {t.catalog.author}
            </label>
            <select
              id="f-author"
              name="author"
              defaultValue={filters.author ?? ""}
              className={fieldClass}
            >
              <option value="">{t.catalog.any}</option>
              {authors.map((a) => (
                <option key={a.id} value={a.slug}>
                  {a.name}
                </option>
              ))}
            </select>
          </div>

          <div>
            <label htmlFor="f-language" className={labelClass}>
              {t.catalog.language}
            </label>
            <select
              id="f-language"
              name="language"
              defaultValue={filters.language ?? ""}
              className={fieldClass}
            >
              <option value="">{t.catalog.any}</option>
              {languageOptions.map((code) => (
                <option key={code} value={code}>
                  {t.bookLanguages[code]}
                </option>
              ))}
            </select>
          </div>

          <fieldset>
            <legend className={labelClass}>{t.catalog.priceRange}</legend>
            <div className="flex gap-2">
              <label className="flex-1">
                <span className="sr-only">{t.catalog.minPrice}</span>
                <input
                  name="minPrice"
                  type="number"
                  min={0}
                  step={100}
                  inputMode="numeric"
                  defaultValue={filters.minPrice}
                  placeholder={t.catalog.minPrice}
                  className={fieldClass}
                />
              </label>
              <label className="flex-1">
                <span className="sr-only">{t.catalog.maxPrice}</span>
                <input
                  name="maxPrice"
                  type="number"
                  min={0}
                  step={100}
                  inputMode="numeric"
                  defaultValue={filters.maxPrice}
                  placeholder={t.catalog.maxPrice}
                  className={fieldClass}
                />
              </label>
            </div>
          </fieldset>

          <label className="flex items-center gap-2 text-sm">
            <input
              type="checkbox"
              name="inStock"
              value="1"
              defaultChecked={filters.inStock}
              className="size-4"
            />
            {t.catalog.inStockOnly}
          </label>

          <div className="flex items-center gap-3">
            <button
              type="submit"
              className="bg-brand hover:bg-brand-strong flex-1 rounded-md px-4 py-2 text-sm font-medium text-white"
            >
              {t.catalog.apply}
            </button>
            <Link href={`/${locale}/books`} className="text-muted hover:text-foreground text-sm">
              {t.catalog.reset}
            </Link>
          </div>
        </form>
      </details>
    </aside>
  );
}
