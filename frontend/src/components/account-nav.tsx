import Link from "next/link";
import { format, getDictionary, getLocale } from "@/i18n/dictionaries";
import { logout } from "@/lib/actions/auth";
import { getSessionUser } from "@/lib/auth/session";
import { GuestCartCleaner } from "./form-bits";

/** Cart link + sign-in/out. Reads the session cookie, so it renders inside <Suspense> in the header. */
export async function AccountNav() {
  const t = await getDictionary();
  const locale = await getLocale();
  const user = await getSessionUser();

  return (
    <div className="flex items-center gap-4 text-sm">
      <Link href={`/${locale}/cart`} className="hover:text-brand font-medium">
        {t.nav.cart}
      </Link>
      {user ? (
        <>
          <GuestCartCleaner />
          {user.roles.includes("Admin") && (
            <Link href={`/${locale}/admin`} className="text-brand font-medium">
              {t.admin.title}
            </Link>
          )}
          <Link href={`/${locale}/account`} className="hover:text-brand font-medium">
            <span className="hidden sm:inline">{format(t.auth.hello, { name: user.name })}</span>
            <span className="sm:hidden">{t.account.title}</span>
          </Link>
          <form action={logout}>
            <input type="hidden" name="locale" value={locale} />
            <button type="submit" className="text-muted hover:text-foreground underline">
              {t.auth.logout}
            </button>
          </form>
        </>
      ) : (
        <Link href={`/${locale}/login`} className="hover:text-brand font-medium">
          {t.nav.login}
        </Link>
      )}
    </div>
  );
}
