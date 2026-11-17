"use client";

import Link from "next/link";
import { useState, useTransition } from "react";
import { addToCart } from "@/lib/actions/cart";
import { addToGuestCart } from "@/lib/guest-cart";

type Labels = {
  quantity: string;
  addToCart: string;
  adding: string;
  added: string;
  viewCart: string;
  error: string;
};

/**
 * Signed-in users: Server Action → account cart. Guests: the action answers "guest" and the book
 * goes into browser storage instead. Either way the customer sees the same confirmation.
 */
export function AddToCartButton({
  bookId,
  maxQuantity,
  cartHref,
  labels,
}: {
  bookId: string;
  maxQuantity: number;
  cartHref: string;
  labels: Labels;
}) {
  const [quantity, setQuantity] = useState(1);
  const [state, setState] = useState<"idle" | "added" | "error">("idle");
  const [pending, startTransition] = useTransition();
  const options = Math.max(1, Math.min(maxQuantity, 10));

  const add = () =>
    startTransition(async () => {
      const result = await addToCart(bookId, quantity);
      if (result.status === "guest") {
        addToGuestCart(bookId, quantity);
      }
      setState(result.status === "error" ? "error" : "added");
    });

  return (
    <div className="flex flex-col gap-2">
      <div className="flex items-center gap-3">
        <label className="sr-only" htmlFor="qty">
          {labels.quantity}
        </label>
        <select
          id="qty"
          value={quantity}
          onChange={(e) => setQuantity(Number(e.target.value))}
          className="border-border bg-background rounded-md border px-3 py-3"
        >
          {Array.from({ length: options }, (_, i) => (
            <option key={i + 1} value={i + 1}>
              {i + 1}
            </option>
          ))}
        </select>
        <button
          type="button"
          onClick={add}
          disabled={pending}
          className="bg-brand hover:bg-brand-strong rounded-md px-6 py-3 font-medium text-white disabled:opacity-60"
        >
          {pending ? labels.adding : labels.addToCart}
        </button>
      </div>
      <p role="status" aria-live="polite" className="text-sm">
        {state === "added" && (
          <>
            <span className="text-brand font-medium">✓ {labels.added}</span>{" "}
            <Link href={cartHref} className="underline">
              {labels.viewCart}
            </Link>
          </>
        )}
        {state === "error" && <span className="text-accent">{labels.error}</span>}
      </p>
    </div>
  );
}
