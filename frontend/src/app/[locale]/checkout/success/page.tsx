import type { Metadata } from "next";
import { Suspense } from "react";
import { OrderResult } from "@/components/order-result";
import { getDictionary, getLocale } from "@/i18n/dictionaries";

export async function generateMetadata(): Promise<Metadata> {
  const t = await getDictionary();
  return { title: t.order.pendingTitle, robots: { index: false } };
}

/** Chargily's success_url. Shows the real order status (confirmed by webhook), not just "success". */
export default async function PaymentSuccessPage({
  searchParams,
}: PageProps<"/[locale]/checkout/success">) {
  const t = await getDictionary();
  return (
    <Suspense fallback={<p className="text-muted">{t.loading}</p>}>
      <Result searchParams={searchParams} />
    </Suspense>
  );
}

async function Result({
  searchParams,
}: Pick<PageProps<"/[locale]/checkout/success">, "searchParams">) {
  const { order } = await searchParams;
  return (
    <OrderResult
      orderId={typeof order === "string" ? order : undefined}
      locale={await getLocale()}
      t={await getDictionary()}
      returnedFrom="success"
    />
  );
}
