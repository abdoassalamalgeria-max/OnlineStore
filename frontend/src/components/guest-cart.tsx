"use client";

import Link from "next/link";
import { useEffect, useState } from "react";
import type { Locale } from "@/i18n/config";
import type { Dictionary } from "@/i18n/messages";
import { onGuestCartChange, readGuestCart, setGuestQuantity } from "@/lib/guest-cart";
import { CartView, quantityOptions, type Cart } from "./cart-view";

const emptyCart: Cart = { items: [], itemCount: 0, subtotal: 0, hasUnavailableItems: false };

/** Guest cart: lines live in localStorage; prices come from the API via /api/cart/preview. */
export function GuestCart({ locale, t }: { locale: Locale; t: Dictionary }) {
  const [cart, setCart] = useState<Cart | null>(null);
  const [failed, setFailed] = useState(false);

  useEffect(() => {
    let cancelled = false;
    const load = async () => {
      const items = readGuestCart();
      if (items.length === 0) {
        setCart(emptyCart);
        return;
      }
      try {
        const response = await fetch("/api/cart/preview", {
          method: "POST",
          headers: { "Content-Type": "application/json" },
          body: JSON.stringify({ items }),
        });
        if (!response.ok) throw new Error(String(response.status));
        const priced = (await response.json()) as Cart;
        if (!cancelled) {
          setCart(priced);
          setFailed(false);
        }
      } catch {
        if (!cancelled) setFailed(true);
      }
    };
    void load();
    const stop = onGuestCartChange(() => void load());
    return () => {
      cancelled = true;
      stop();
    };
  }, []);

  if (failed) {
    return (
      <p role="alert" className="text-muted rounded-lg border p-6 text-center">
        {t.errors.text}
      </p>
    );
  }

  if (!cart) {
    return (
      <p role="status" className="text-muted py-10 text-center">
        {t.loading}
      </p>
    );
  }

  const returnTo = encodeURIComponent(`/${locale}/checkout`);

  return (
    <CartView
      cart={cart}
      locale={locale}
      t={t}
      controls={(line) => (
        <div className="flex items-center gap-3">
          <label className="sr-only" htmlFor={`qty-${line.bookId}`}>
            {t.cart.quantity}
          </label>
          <select
            id={`qty-${line.bookId}`}
            value={line.quantity}
            onChange={(e) => setGuestQuantity(line.bookId, Number(e.target.value))}
            className="border-border bg-background rounded-md border px-2 py-1"
          >
            {quantityOptions(line.stock, line.quantity).map((n) => (
              <option key={n} value={n}>
                {n}
              </option>
            ))}
          </select>
          <button
            type="button"
            onClick={() => setGuestQuantity(line.bookId, 0)}
            className="text-muted hover:text-accent text-sm underline"
          >
            {t.cart.remove}
          </button>
        </div>
      )}
      footer={
        <Link
          href={`/${locale}/login?returnTo=${returnTo}`}
          className="bg-brand hover:bg-brand-strong rounded-md px-5 py-2.5 text-center font-medium text-white"
        >
          {t.cart.loginToCheckout}
        </Link>
      }
    />
  );
}
