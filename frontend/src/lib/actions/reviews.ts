"use server";

import { updateTag } from "next/cache";
import { getAccessToken } from "@/lib/auth/session";
import { apiWithToken, fieldErrorsFrom, text, type FormState } from "./shared";

export type ReviewFormState = (FormState & { ok?: boolean }) | null;

/** Cached data that shows a book's rating; expired so the author sees their review immediately. */
function expireBook(slug: string) {
  updateTag(`reviews:${slug}`);
  updateTag(`book:${slug}`);
}

export async function saveReview(
  _prev: ReviewFormState,
  formData: FormData,
): Promise<ReviewFormState> {
  const token = await getAccessToken();
  if (!token) return { error: "signInToReview" };

  const slug = text(formData, "slug");
  const rating = Number(text(formData, "rating"));
  if (!Number.isInteger(rating) || rating < 1 || rating > 5) return { error: "chooseRating" };

  const api = await apiWithToken(token);
  const { data, error, response } = await api.PUT("/api/v1/books/{slug}/reviews/mine", {
    params: { path: { slug } },
    body: { rating, comment: text(formData, "comment") || null },
  });

  if (data) {
    expireBook(slug);
    return { ok: true };
  }
  if (response.status === 403) return { error: "buyersOnly" };
  if (response.status === 400)
    return { error: "chooseRating", fieldErrors: fieldErrorsFrom(error) };
  return { error: "genericError" };
}

export async function deleteReview(formData: FormData) {
  const token = await getAccessToken();
  if (!token) return;

  const slug = text(formData, "slug");
  const api = await apiWithToken(token);
  await api.DELETE("/api/v1/books/{slug}/reviews/mine", { params: { path: { slug } } });
  expireBook(slug);
}
