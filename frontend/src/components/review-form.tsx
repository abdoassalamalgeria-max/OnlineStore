"use client";

import { useActionState, useState } from "react";
import type { Dictionary } from "@/i18n/messages";
import { deleteReview, saveReview } from "@/lib/actions/reviews";
import { inputClass, SubmitButton } from "./form-bits";

type Props = {
  slug: string;
  initial: { rating: number; comment: string | null } | null;
  t: Dictionary["reviews"];
  loading: string;
  genericError: string;
};

/** Star picker (radio group, keyboard-accessible) + comment; creates or edits the user's review. */
export function ReviewForm({ slug, initial, t, loading, genericError }: Props) {
  const [state, action] = useActionState(saveReview, null);
  const [rating, setRating] = useState(initial?.rating ?? 0);

  const message = state?.ok
    ? t.saved
    : state?.error
      ? (t[state.error as keyof typeof t] ?? genericError)
      : null;

  return (
    <div className="border-border bg-surface flex flex-col gap-3 rounded-lg border p-4">
      <h3 className="font-bold">{t.yourReview}</h3>
      <form action={action} className="flex flex-col gap-3">
        <input type="hidden" name="slug" value={slug} />
        <fieldset>
          <legend className="mb-1 text-sm font-medium">{t.rating}</legend>
          <div className="flex gap-1" dir="ltr">
            {[1, 2, 3, 4, 5].map((star) => (
              <label key={star} className="cursor-pointer text-3xl leading-none">
                <input
                  type="radio"
                  name="rating"
                  value={star}
                  checked={rating === star}
                  onChange={() => setRating(star)}
                  className="peer sr-only"
                />
                <span
                  aria-hidden="true"
                  className={`peer-focus-visible:outline-brand rounded peer-focus-visible:outline-2 ${star <= rating ? "text-accent" : "text-border"}`}
                >
                  ★
                </span>
                <span className="sr-only">{t.stars.replace("{count}", String(star))}</span>
              </label>
            ))}
          </div>
        </fieldset>
        <div>
          <label htmlFor="review-comment" className="mb-1 block text-sm font-medium">
            {t.comment}
          </label>
          <textarea
            id="review-comment"
            name="comment"
            rows={3}
            maxLength={2000}
            defaultValue={initial?.comment ?? ""}
            className={inputClass}
          />
        </div>
        {message && (
          <p role="status" className={state?.ok ? "text-brand text-sm" : "text-accent text-sm"}>
            {message}
          </p>
        )}
        <div className="w-full sm:w-48">
          <SubmitButton pendingLabel={loading}>{initial ? t.update : t.submit}</SubmitButton>
        </div>
      </form>
      {initial && (
        <form action={deleteReview}>
          <input type="hidden" name="slug" value={slug} />
          <button type="submit" className="text-muted hover:text-accent text-sm underline">
            {t.delete}
          </button>
        </form>
      )}
    </div>
  );
}
