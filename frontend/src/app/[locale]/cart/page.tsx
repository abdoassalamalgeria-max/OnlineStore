import type { Metadata } from "next";
import Link from "next/link";
import { Suspense } from "react";
import { AutoSubmitSelect } from "@/components/auto-submit-select";
import { CartView, quantityOptions } from "@/components/cart-view";
import { GuestCart } from "@/components/guest-cart";
import { getDictionary, getLocale } from "@/i18n/dictionaries";
import { removeCartLine, updateCartLine } from "@/lib/actions/cart";
import { userApi } from "@/lib/api/server-client";
import { ApiError } from "@/lib/api/client";
import { getAccessToken } from "@/lib/auth/session";

export async function generateMetadata(): Promise<Metadata> {
  const t = await getDictionary();
  return { title: t.cart.title, robots: { index: false } };
}

export default async function CartPage() {
  const t = await getDictionary();
  return (
    <div className="flex flex-col gap-6">
      <h1 className="text-3xl font-bold">{t.cart.title}</h1>
      <Suspense fallback={<p className="text-muted">{t.loading}</p>}>
        <CartContent />
      </Suspense>
    </div>
  );
}

async function CartContent() {
  const t = await getDictionary();
  const locale = await getLocale();

  if (!(await getAccessToken())) {
    return <GuestCart locale={locale} t={t} />;
  }

  const api = await userApi();
  const { data: cart, response } = await api.GET("/api/v1/cart");
  if (!cart) {
    throw new ApiError(response.status, "/api/v1/cart");
  }

  return (
    <CartView
      cart={cart}
      locale={locale}
      t={t}
      controls={(line) => (
        <div className="flex items-center gap-3">
          {/* Each control is a tiny Server Action form: works without JavaScript too. */}
          <form action={updateCartLine} className="flex items-center gap-2">
            <input type="hidden" name="bookId" value={line.bookId} />
            <label className="sr-only" htmlFor={`qty-${line.bookId}`}>
              {t.cart.quantity}
            </label>
            <AutoSubmitSelect
              id={`qty-${line.bookId}`}
              name="quantity"
              defaultValue={line.quantity}
              className="border-border bg-background rounded-md border px-2 py-1"
            >
              {quantityOptions(line.stock, line.quantity).map((n) => (
                <option key={n} value={n}>
                  {n}
                </option>
              ))}
            </AutoSubmitSelect>
            <noscript>
              <button type="submit" className="text-sm underline">
                OK
              </button>
            </noscript>
          </form>
          <form action={removeCartLine}>
            <input type="hidden" name="bookId" value={line.bookId} />
            <button type="submit" className="text-muted hover:text-accent text-sm underline">
              {t.cart.remove}
            </button>
          </form>
        </div>
      )}
      footer={
        cart.hasUnavailableItems ? (
          <span className="bg-brand/40 cursor-not-allowed rounded-md px-5 py-2.5 text-center font-medium text-white">
            {t.cart.checkout}
          </span>
        ) : (
          <Link
            href={`/${locale}/checkout`}
            className="bg-brand hover:bg-brand-strong rounded-md px-5 py-2.5 text-center font-medium text-white"
          >
            {t.cart.checkout}
          </Link>
        )
      }
    />
  );
}
