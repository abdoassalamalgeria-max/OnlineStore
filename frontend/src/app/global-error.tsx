"use client"; // Replaces the root layout when it fails, so it renders its own <html>.

import { errorMessages } from "@/i18n/error-messages";

export default function GlobalError({ retry }: { error: unknown; retry: () => void }) {
  const t = errorMessages.ar;
  return (
    <html lang="ar" dir="rtl">
      <body
        style={{ fontFamily: "system-ui, sans-serif", textAlign: "center", padding: "4rem 1rem" }}
      >
        <h1>{t.title}</h1>
        <p>{t.text}</p>
        <p lang="fr" dir="ltr">
          {errorMessages.fr.text}
        </p>
        <button type="button" onClick={() => retry()}>
          {t.retry}
        </button>
      </body>
    </html>
  );
}
