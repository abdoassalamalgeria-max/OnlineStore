import assert from "node:assert/strict";
import { test } from "node:test";
import { catalogHref, catalogPageSize, parseCatalogParams } from "./catalog-params.ts";

test("parses a full query string", () => {
  const filters = parseCatalogParams({
    q: "  الأمير ",
    category: "novels",
    author: "waciny-laredj",
    language: "ar",
    minPrice: "500",
    maxPrice: "3000",
    inStock: "1",
    sort: "PriceAsc",
    page: "3",
  });

  assert.deepEqual(filters, {
    search: "الأمير",
    category: "novels",
    author: "waciny-laredj",
    language: "ar",
    minPrice: 500,
    maxPrice: 3000,
    inStock: true,
    sort: "PriceAsc",
    page: 3,
    pageSize: catalogPageSize,
  });
});

test("drops invalid values instead of failing", () => {
  const filters = parseCatalogParams({
    language: "xx",
    minPrice: "-5",
    maxPrice: "abc",
    sort: "DROP TABLE",
    page: "0",
    inStock: "yes",
  });

  assert.equal(filters.language, undefined);
  assert.equal(filters.minPrice, undefined);
  assert.equal(filters.maxPrice, undefined);
  assert.equal(filters.sort, undefined);
  assert.equal(filters.inStock, undefined);
  assert.equal(filters.page, 1);
});

test("takes the first value of repeated parameters and caps search length", () => {
  const filters = parseCatalogParams({ category: ["a", "b"], q: "x".repeat(500) });

  assert.equal(filters.category, "a");
  assert.equal(filters.search?.length, 100);
});

test("builds clean URLs and round-trips through the parser", () => {
  assert.equal(catalogHref("ar", { page: 1, sort: "Newest" }), "/ar/books");

  const href = catalogHref("fr", { search: "camus", inStock: true, minPrice: 0 }, { page: 2 });
  assert.equal(href, "/fr/books?q=camus&minPrice=0&inStock=1&page=2");

  const parsed = parseCatalogParams(Object.fromEntries(new URL(href, "http://x").searchParams));
  assert.equal(parsed.search, "camus");
  assert.equal(parsed.minPrice, 0);
  assert.equal(parsed.inStock, true);
  assert.equal(parsed.page, 2);
});
