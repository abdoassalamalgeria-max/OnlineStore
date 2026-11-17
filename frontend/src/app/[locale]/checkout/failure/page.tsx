import type { Metadata } from "next";
import { Suspense } from "react";
import { OrderResult } from "@/components/order-result";
import { getDictionary, getLocale } from "@/i18n/dictionaries";

export async function generateMetadata(): Promise<Metadata> {
  const t = await getDictionary();
  return { title: t.order.failureTitle, robots: { index: false } };
}

/** Chargily's failure_url (declined or cancelled payment). */
export default async function PaymentFailurePage({
  searchParams,
}: PageProps<"/[locale]/checkout/failure">) {
  const t = await getDictionary();
  return (
    <Suspense fallback={<p className="text-muted">{t.loading}</p>}>
      <Result searchParams={searchParams} />
    </Suspense>
  );
}

async function Result({
  searchParams,
}: Pick<PageProps<"/[locale]/checkout/failure">, "searchParams">) {
  const { order } = await searchParams;
  return (
    <OrderResult
      orderId={typeof order === "string" ? order : undefined}
      locale={await getLocale()}
      t={await getDictionary()}
      returnedFrom="failure"
    />
  );
}
