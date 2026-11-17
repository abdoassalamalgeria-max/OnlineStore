import type { Locale } from "@/i18n/config";
import { formatDzd } from "@/lib/format";

type Props = {
  price: number;
  salePrice: number | null;
  locale: Locale;
  saleLabel: string;
  size?: "md" | "lg";
};

/** Current price, with the original struck through when the book is on sale. */
export function Price({ price, salePrice, locale, saleLabel, size = "md" }: Props) {
  const text = size === "lg" ? "text-2xl" : "text-base";

  if (salePrice === null) {
    return <p className={`${text} font-bold`}>{formatDzd(price, locale)}</p>;
  }

  return (
    <p className="flex flex-wrap items-baseline gap-x-2">
      <span className={`${text} text-accent font-bold`}>{formatDzd(salePrice, locale)}</span>
      <del className="text-muted text-sm">{formatDzd(price, locale)}</del>
      <span className="bg-accent/10 text-accent rounded px-1.5 text-xs font-medium">
        {saleLabel}
      </span>
    </p>
  );
}
