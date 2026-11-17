"use server";

import { refresh } from "next/cache";
import { getAccessToken } from "@/lib/auth/session";
import { apiWithToken, text } from "./shared";

export type AddToCartResult = { status: "added" } | { status: "guest" } | { status: "error" };

/** Adds to the signed-in user's cart; tells guests to use their browser cart instead. */
export async function addToCart(bookId: string, quantity: number): Promise<AddToCartResult> {
  const token = await getAccessToken();
  if (!token) {
    return { status: "guest" };
  }

  const api = await apiWithToken(token);
  const { response } = await api.POST("/api/v1/cart/items", { body: { bookId, quantity } });
  if (response.status === 401) return { status: "guest" };
  return response.ok ? { status: "added" } : { status: "error" };
}

/** Quantity select on the cart page (0 removes the line). */
export async function updateCartLine(formData: FormData) {
  const token = await getAccessToken();
  const quantity = Number(text(formData, "quantity"));
  if (!token || !Number.isInteger(quantity)) return;

  const api = await apiWithToken(token);
  await api.PUT("/api/v1/cart/items/{bookId}", {
    params: { path: { bookId: text(formData, "bookId") } },
    body: { quantity },
  });
  refresh();
}

export async function removeCartLine(formData: FormData) {
  const token = await getAccessToken();
  if (!token) return;

  const api = await apiWithToken(token);
  await api.DELETE("/api/v1/cart/items/{bookId}", {
    params: { path: { bookId: text(formData, "bookId") } },
  });
  refresh();
}
