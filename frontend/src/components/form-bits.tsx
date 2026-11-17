"use client";

import { useEffect, useRef, type ReactNode } from "react";
import { useRouter } from "next/navigation";
import { useFormStatus } from "react-dom";
import { clearGuestCart, readGuestCart } from "@/lib/guest-cart";

export const inputClass =
  "w-full rounded-md border border-border bg-background px-3 py-2 aria-[invalid=true]:border-accent";

/** Submit button that disables itself and swaps its label while the form's action runs. */
export function SubmitButton({
  children,
  pendingLabel,
}: {
  children: ReactNode;
  pendingLabel: string;
}) {
  const { pending } = useFormStatus();
  return (
    <button
      type="submit"
      disabled={pending}
      aria-disabled={pending}
      className="bg-brand hover:bg-brand-strong w-full rounded-md px-5 py-2.5 font-medium text-white disabled:opacity-60"
    >
      {pending ? pendingLabel : children}
    </button>
  );
}

/** Field-level error text, linked to its input with aria-describedby. */
export function FieldError({ id, messages }: { id: string; messages?: string[] }) {
  if (!messages?.length) return null;
  return (
    <p id={id} className="text-accent mt-1 text-sm">
      {messages.join(" ")}
    </p>
  );
}

/** Hidden input carrying the guest cart (from localStorage) into the login/register action. */
export function GuestCartField() {
  const ref = useRef<HTMLInputElement>(null);
  useEffect(() => {
    const form = ref.current?.form;
    const fill = () => {
      if (ref.current) ref.current.value = JSON.stringify(readGuestCart());
    };
    fill();
    form?.addEventListener("submit", fill);
    return () => form?.removeEventListener("submit", fill);
  }, []);
  return <input ref={ref} type="hidden" name="guestCart" defaultValue="[]" />;
}

/** Rendered only for signed-in users: the guest cart has been merged at sign-in, so drop it. */
export function GuestCartCleaner() {
  useEffect(() => {
    if (readGuestCart().length > 0) clearGuestCart();
  }, []);
  return null;
}

/** Re-fetches the page every few seconds (payment confirmation), up to a limit. */
export function AutoRefresh({ seconds, maxTimes }: { seconds: number; maxTimes: number }) {
  const router = useRouter();
  const count = useRef(0);
  useEffect(() => {
    const timer = setInterval(() => {
      count.current += 1;
      if (count.current > maxTimes) {
        clearInterval(timer);
        return;
      }
      router.refresh();
    }, seconds * 1000);
    return () => clearInterval(timer);
  }, [router, seconds, maxTimes]);
  return null;
}
