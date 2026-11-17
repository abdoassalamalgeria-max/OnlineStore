"use client";

import Link from "next/link";
import { useActionState, useState } from "react";
import type { Locale } from "@/i18n/config";
import type { Dictionary } from "@/i18n/messages";
import { placeOrder } from "@/lib/actions/checkout";
import type { Schemas } from "@/lib/api/client";
import { formatDzd } from "@/lib/format";
import { FieldError, inputClass, SubmitButton } from "./form-bits";

type Props = {
  locale: Locale;
  t: Dictionary;
  subtotal: number;
  itemCount: number;
  wilayas: Schemas["WilayaDto"][];
  defaults: { fullName: string; phone: string };
};

/** Address form + live total (shipping depends on the chosen wilaya). Submits to the placeOrder action. */
export function CheckoutForm({ locale, t, subtotal, itemCount, wilayas, defaults }: Props) {
  const [state, action] = useActionState(placeOrder, null);
  const [wilayaCode, setWilayaCode] = useState("");
  const wilaya = wilayas.find((w) => String(w.code) === wilayaCode);
  const errors = state?.fieldErrors ?? {};
  const invalid = (name: string) => (errors[name] ? true : undefined);
  const c = t.checkout;

  const field = (name: string, label: string, input: React.ReactNode, hint?: string) => (
    <div>
      <label htmlFor={name} className="mb-1 block text-sm font-medium">
        {label}
      </label>
      {input}
      {hint && (
        <p id={`${name}-hint`} className="text-muted mt-1 text-xs">
          {hint}
        </p>
      )}
      <FieldError id={`${name}-error`} messages={errors[name]} />
    </div>
  );

  return (
    <form action={action} className="grid gap-8 lg:grid-cols-[1fr_20rem]" noValidate>
      <input type="hidden" name="locale" value={locale} />

      <fieldset className="border-border bg-surface flex flex-col gap-4 rounded-lg border p-4">
        <legend className="px-1 text-lg font-bold">{c.shippingTitle}</legend>

        {state?.error && (
          <div
            role="alert"
            className="border-accent/40 bg-accent/10 text-accent rounded-md border p-3 text-sm"
          >
            {c[state.error as keyof typeof c] ?? c.gatewayError}{" "}
            {state.error === "stockError" && (
              <Link href={`/${locale}/cart`} className="underline">
                {t.order.backToCart}
              </Link>
            )}
          </div>
        )}

        {field(
          "fullName",
          c.fullName,
          <input
            id="fullName"
            name="fullName"
            autoComplete="name"
            required
            defaultValue={defaults.fullName}
            aria-invalid={invalid("fullName")}
            aria-describedby="fullName-error"
            className={inputClass}
          />,
        )}
        {field(
          "phone",
          c.phone,
          <input
            id="phone"
            name="phone"
            type="tel"
            inputMode="tel"
            autoComplete="tel"
            required
            dir="ltr"
            defaultValue={defaults.phone}
            aria-invalid={invalid("phone")}
            aria-describedby="phone-hint phone-error"
            className={inputClass}
          />,
          c.phoneHint,
        )}
        {field(
          "wilayaCode",
          c.wilaya,
          <select
            id="wilayaCode"
            name="wilayaCode"
            required
            value={wilayaCode}
            onChange={(e) => setWilayaCode(e.target.value)}
            aria-invalid={invalid("wilayaCode")}
            aria-describedby="wilayaCode-error"
            className={inputClass}
          >
            <option value="">{c.chooseWilaya}</option>
            {wilayas
              .filter((w) => w.isDeliveryAvailable)
              .map((w) => (
                <option key={w.code} value={w.code}>
                  {String(w.code).padStart(2, "0")} - {locale === "ar" ? w.nameAr : w.nameFr}
                </option>
              ))}
          </select>,
        )}
        {field(
          "commune",
          c.commune,
          <input
            id="commune"
            name="commune"
            required
            aria-invalid={invalid("commune")}
            aria-describedby="commune-error"
            className={inputClass}
          />,
        )}
        {field(
          "addressLine",
          c.addressLine,
          <textarea
            id="addressLine"
            name="addressLine"
            required
            rows={2}
            autoComplete="street-address"
            aria-invalid={invalid("addressLine")}
            aria-describedby="addressLine-hint addressLine-error"
            className={inputClass}
          />,
          c.addressHint,
        )}
      </fieldset>

      <aside className="border-border bg-surface flex h-fit flex-col gap-3 rounded-lg border p-4">
        <h2 className="text-lg font-bold">{c.summary}</h2>
        <dl className="flex flex-col gap-2 text-sm">
          <div className="flex justify-between">
            <dt>
              {t.cart.subtotal} ({itemCount})
            </dt>
            <dd>{formatDzd(subtotal, locale)}</dd>
          </div>
          <div className="flex justify-between">
            <dt>{c.shipping}</dt>
            <dd>{wilaya ? formatDzd(wilaya.shippingFee, locale) : "—"}</dd>
          </div>
          <div className="border-border flex justify-between border-t pt-2 text-base font-bold">
            <dt>{c.total}</dt>
            <dd aria-live="polite">{formatDzd(subtotal + (wilaya?.shippingFee ?? 0), locale)}</dd>
          </div>
        </dl>
        <SubmitButton pendingLabel={c.paying}>{c.pay}</SubmitButton>
        <p className="text-muted text-xs">{c.payNote}</p>
      </aside>
    </form>
  );
}
