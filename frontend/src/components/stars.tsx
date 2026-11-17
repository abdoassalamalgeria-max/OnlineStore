/**
 * Read-only star rating. The visual stars are decorative; screen readers get "4.5 out of 5".
 * Stars always fill left-to-right visually (dir="ltr"), as is conventional even in RTL layouts.
 */
export function Stars({
  value,
  label,
  size = "sm",
}: {
  value: number;
  label: string;
  size?: "sm" | "lg";
}) {
  const percent = Math.max(0, Math.min(100, (value / 5) * 100));
  const text = size === "lg" ? "text-2xl" : "text-sm";
  return (
    <span
      role="img"
      aria-label={label}
      dir="ltr"
      className={`relative inline-block leading-none ${text}`}
    >
      <span aria-hidden="true" className="text-border">
        ★★★★★
      </span>
      <span
        aria-hidden="true"
        className="text-accent absolute inset-y-0 left-0 overflow-hidden whitespace-nowrap"
        style={{ width: `${percent}%` }}
      >
        ★★★★★
      </span>
    </span>
  );
}
