import Link from "next/link";
import type { ReactNode } from "react";
import type { Locale } from "@/i18n/config";
import { format, type Dictionary } from "@/i18n/messages";
import type { Schemas } from "@/lib/api/client";
import { formatDzd } from "@/lib/format";
import { BookCover } from "./book-cover";

export type Cart = Schemas["CartDto"];
export type CartLine = Schemas["CartLineDto"];

type Props = {
  cart: Cart;
  locale: Locale;
  t: Dictionary;
  /** Quantity/remove controls per line (Server Action forms for accounts, local state for guests). */
  controls: (line: CartLine) => ReactNode;
  /** Checkout button or sign-in prompt. */
  footer: ReactNode;
};

/** Presentational cart, shared by the account cart (server) and the guest cart (client). */
export function CartView({ cart, locale, t, controls, footer }: Props) {
  if (cart.items.length === 0) {
    return (
      <div className="border-border flex flex-col items-center gap-4 rounded-lg border border-dashed p-10 text-center">
        <p>{t.cart.empty}</p>
        <Link href={`/${locale}/books`} className="text-brand font-medium underline">
          {t.cart.continueShopping}
        </Link>
      </div>
    );
  }

  return (
    <div className="grid gap-8 lg:grid-cols-[1fr_20rem]">
      <ul className="divide-border border-border bg-surface divide-y rounded-lg border">
        {cart.items.map((line) => (
          <li key={line.bookId} className="flex gap-4 p-4">
            <Link
              href={`/${locale}/books/${line.slug}`}
              className="relative aspect-[2/3] w-16 shrink-0"
            >
              <BookCover
                title={line.title}
                url={line.coverImageUrl}
                language={line.language}
                sizes="4rem"
              />
            </Link>
            <div className="flex flex-1 flex-col gap-2">
              <Link
                href={`/${locale}/books/${line.slug}`}
                lang={line.language}
                dir="auto"
                className="hover:text-brand font-bold"
              >
                {line.title}
              </Link>
              <p className="text-muted text-sm">
                {t.cart.unitPrice}: {formatDzd(line.unitPrice, locale)}
              </p>
              {!line.available && (
                <p className="text-accent text-sm font-medium">
                  {format(t.cart.unavailable, { stock: line.stock })}
                </p>
              )}
              <div className="mt-auto flex flex-wrap items-center justify-between gap-3">
                {controls(line)}
                <p className="font-bold">{formatDzd(line.lineTotal, locale)}</p>
              </div>
            </div>
          </li>
        ))}
      </ul>

      <aside className="border-border bg-surface flex h-fit flex-col gap-4 rounded-lg border p-4">
        <div className="flex justify-between font-bold">
          <span>{t.cart.subtotal}</span>
          <span>{formatDzd(cart.subtotal, locale)}</span>
        </div>
        <p className="text-muted text-sm">{t.cart.shippingNote}</p>
        {cart.hasUnavailableItems && <p className="text-accent text-sm">{t.cart.fixUnavailable}</p>}
        {footer}
      </aside>
    </div>
  );
}

export const quantityOptions = (stock: number, current: number) =>
  Array.from({ length: Math.max(current, Math.min(Math.max(stock, 1), 20)) }, (_, i) => i + 1);
