import type { Metadata } from "next";
import Link from "next/link";
import { notFound, redirect } from "next/navigation";
import { Suspense, type ReactNode } from "react";
import { getDictionary, getLocale } from "@/i18n/dictionaries";
import { getSessionUser } from "@/lib/auth/session";

export async function generateMetadata(): Promise<Metadata> {
  const t = await getDictionary();
  return {
    title: { default: t.admin.title, template: `%s | ${t.admin.title}` },
    robots: { index: false },
  };
}

/**
 * Admin area. The navigation is static; the role check reads the session cookie, so it sits inside
 * <Suspense>. This is a UX gate only — the API rejects non-admins on every admin endpoint.
 */
export default async function AdminLayout({ children }: LayoutProps<"/[locale]/admin">) {
  const t = await getDictionary();
  const locale = await getLocale();
  const n = t.admin.nav;
  const links: [string, string][] = [
    ["", n.overview],
    ["/books", n.books],
    ["/orders", n.orders],
    ["/authors", n.authors],
    ["/categories", n.categories],
    ["/wilayas", n.wilayas],
    ["/reviews", n.reviews],
  ];

  return (
    <div className="flex flex-col gap-6">
      <nav aria-label={t.admin.title} className="border-border overflow-x-auto border-b pb-2">
        <ul className="flex gap-4 text-sm font-medium whitespace-nowrap">
          {links.map(([path, label]) => (
            <li key={path}>
              <Link href={`/${locale}/admin${path}`} className="hover:text-brand">
                {label}
              </Link>
            </li>
          ))}
        </ul>
      </nav>
      <Suspense fallback={<p className="text-muted">{t.loading}</p>}>
        <AdminGate locale={locale}>{children}</AdminGate>
      </Suspense>
    </div>
  );
}

async function AdminGate({ locale, children }: { locale: string; children: ReactNode }) {
  const user = await getSessionUser();
  if (!user) {
    redirect(`/${locale}/login?returnTo=${encodeURIComponent(`/${locale}/admin`)}`);
  }
  if (!user.roles.includes("Admin")) {
    notFound(); // don't advertise that an admin area exists
  }
  return <>{children}</>;
}
