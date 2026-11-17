"use client"; // Error boundaries must be Client Components

import { useParams } from "next/navigation";
import { useEffect } from "react";
import { defaultLocale, isLocale } from "@/i18n/config";
import { errorMessages } from "@/i18n/error-messages";

/** Catches rendering/data errors in any page under /[locale] (e.g. the API is unreachable). */
export default function ErrorPage({ error, retry }: { error: unknown; retry: () => void }) {
  const params = useParams<{ locale: string }>();
  const t = errorMessages[isLocale(params.locale) ? params.locale : defaultLocale];

  useEffect(() => {
    console.error(error);
  }, [error]);

  return (
    <div role="alert" className="flex flex-col items-center gap-4 py-20 text-center">
      <h1 className="text-2xl font-bold">{t.title}</h1>
      <p className="text-muted">{t.text}</p>
      <button
        type="button"
        onClick={() => retry()}
        className="bg-brand hover:bg-brand-strong rounded-md px-5 py-2.5 font-medium text-white"
      >
        {t.retry}
      </button>
    </div>
  );
}
