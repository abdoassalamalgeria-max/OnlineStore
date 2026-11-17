import type { Metadata } from "next";
import { redirect } from "next/navigation";
import { Suspense } from "react";
import { CheckoutForm } from "@/components/checkout-form";
import { getDictionary, getLocale } from "@/i18n/dictionaries";
import { ApiError, api } from "@/lib/api/client";
import { userApi } from "@/lib/api/server-client";
import { getAccessToken } from "@/lib/auth/session";

export async function generateMetadata(): Promise<Metadata> {
  const t = await getDictionary();
  return { title: t.checkout.title, robots: { index: false } };
}

export default async function CheckoutPage() {
  const t = await getDictionary();
  return (
    <div className="flex flex-col gap-6">
      <h1 className="text-3xl font-bold">{t.checkout.title}</h1>
      <Suspense fallback={<p className="text-muted">{t.loading}</p>}>
        <CheckoutContent />
      </Suspense>
    </div>
  );
}

async function CheckoutContent() {
  const t = await getDictionary();
  const locale = await getLocale();

  if (!(await getAccessToken())) {
    redirect(`/${locale}/login?returnTo=${encodeURIComponent(`/${locale}/checkout`)}`);
  }

  const client = await userApi();
  const [cartResult, meResult, wilayasResult] = await Promise.all([
    client.GET("/api/v1/cart"),
    client.GET("/api/v1/auth/me"),
    api.GET("/api/v1/wilayas"),
  ]);

  if (!cartResult.data) throw new ApiError(cartResult.response.status, "/api/v1/cart");
  if (!wilayasResult.data) throw new ApiError(wilayasResult.response.status, "/api/v1/wilayas");

  const cart = cartResult.data;
  if (cart.items.length === 0 || cart.hasUnavailableItems) {
    redirect(`/${locale}/cart`);
  }

  return (
    <CheckoutForm
      locale={locale}
      t={t}
      subtotal={cart.subtotal}
      itemCount={cart.itemCount}
      wilayas={wilayasResult.data}
      defaults={{
        fullName: meResult.data?.fullName ?? "",
        phone: meResult.data?.phoneNumber ?? "",
      }}
    />
  );
}
