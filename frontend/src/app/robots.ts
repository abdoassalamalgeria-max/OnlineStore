import type { MetadataRoute } from "next";
import { connection } from "next/server";
import { locales } from "@/i18n/config";

/** /robots.txt: public catalog crawlable; account, cart, checkout and admin are not. */
export default async function robots(): Promise<MetadataRoute.Robots> {
  // SITE_URL is a runtime setting (Docker env), so don't freeze it into the build.
  await connection();
  const base = (process.env.SITE_URL ?? "http://localhost:3000").replace(/\/$/, "");
  const privatePaths = ["account", "admin", "cart", "checkout", "login", "register"];

  return {
    rules: {
      userAgent: "*",
      allow: "/",
      disallow: ["/api/", ...privatePaths.flatMap((p) => locales.map((l) => `/${l}/${p}`))],
    },
    sitemap: `${base}/sitemap.xml`,
  };
}
