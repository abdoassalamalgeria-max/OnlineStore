import type { Dictionary } from "@/i18n/messages";
import type { Schemas } from "@/lib/api/client";

type Status = Schemas["OrderStatus"];

const styles: Record<Status, string> = {
  Pending: "bg-border text-foreground",
  Paid: "bg-brand/15 text-brand",
  Shipped: "bg-brand/15 text-brand",
  Delivered: "bg-brand text-white",
  Cancelled: "bg-accent/10 text-accent",
  Failed: "bg-accent/10 text-accent",
};

export function OrderStatusBadge({ status, t }: { status: Status; t: Dictionary }) {
  return (
    <span
      className={`inline-block rounded-full px-2.5 py-0.5 text-xs font-medium ${styles[status]}`}
    >
      {t.order.status[status]}
    </span>
  );
}
