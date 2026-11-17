"use server";

import { redirect } from "next/navigation";
import { toLocale } from "@/i18n/messages";
import { getAccessToken } from "@/lib/auth/session";
import { apiWithToken, fieldErrorsFrom, text, type FormState } from "./shared";

/**
 * Places the order and sends the customer to Chargily's hosted payment page. The order is only
 * marked paid later, when Chargily's webhook reaches the API.
 */
export async function placeOrder(_prev: FormState, formData: FormData): Promise<FormState> {
  const locale = toLocale(formData.get("locale"));
  const token = await getAccessToken();
  if (!token) {
    redirect(`/${locale}/login?returnTo=${encodeURIComponent(`/${locale}/checkout`)}`);
  }

  const api = await apiWithToken(token);
  const { data, error, response } = await api.POST("/api/v1/checkout", {
    body: {
      locale,
      shippingAddress: {
        fullName: text(formData, "fullName"),
        phone: text(formData, "phone"),
        wilayaCode: Number(text(formData, "wilayaCode")) || 0,
        commune: text(formData, "commune"),
        addressLine: text(formData, "addressLine"),
      },
    },
  });

  if (data) {
    // External redirect to the payment page (an absolute https URL returned by the API).
    redirect(data.paymentUrl);
  }

  switch (response.status) {
    case 400:
      return { error: "fixErrors", fieldErrors: fieldErrorsFrom(error) };
    case 401:
      redirect(`/${locale}/login?returnTo=${encodeURIComponent(`/${locale}/checkout`)}`);
    case 409:
      return { error: "stockError" };
    case 502:
      return { error: "gatewayError" };
    default:
      return { error: "gatewayError" };
  }
}
