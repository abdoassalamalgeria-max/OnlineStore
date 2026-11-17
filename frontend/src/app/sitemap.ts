import type { MetadataRoute } from "next";
import { connection } from "next/server";
import { locales } from "@/i18n/config";
import { getBooks } from "@/lib/api/catalog";

const site = () => (process.env.SITE_URL ?? "http://localhost:3000").replace(/\/$/, "");

/** One entry per page and language, each listing its ar/fr/en alternates (hreflang). */
function entry(path: string, priority: number): MetadataRoute.Sitemap[number] {
  const base = site();
  return {
    url: `${base}/ar${path}`,
    changeFrequency: "daily",
    priority,
    alternates: {
      languages: Object.fromEntries(locales.map((l) => [l, `${base}/${l}${path}`])),
    },
  };
}

/**
 * /sitemap.xml: home, catalog and every book. Rendered per request (the build must not need the
 * API); the book list itself comes from the cached catalog fetcher, so this stays cheap.
 */
export default async function sitemap(): Promise<MetadataRoute.Sitemap> {
  await connection();

  const slugs: string[] = [];
  try {
    for (let page = 1; page <= 50; page++) {
      const result = await getBooks({ page, pageSize: 48, sort: "Newest" });
      slugs.push(...result.items.map((b) => b.slug));
      if (page * 48 >= result.totalCount) break;
    }
  } catch {
    // API down: still serve the static pages rather than an error.
  }

  return [
    entry("", 1),
    entry("/books", 0.8),
    ...slugs.map((slug) => entry(`/books/${encodeURIComponent(slug)}`, 0.6)),
  ];
}
