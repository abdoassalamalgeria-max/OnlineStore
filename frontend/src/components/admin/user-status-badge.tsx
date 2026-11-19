import type { Dictionary } from "@/i18n/dictionaries";

/** Active / suspended pill for the admin users pages. */
export function UserStatusBadge({ suspended, t }: { suspended: boolean; t: Dictionary }) {
  return suspended ? (
    <span className="bg-accent/10 text-accent rounded-full px-2 py-0.5 text-xs font-bold">
      {t.admin.users.suspended}
    </span>
  ) : (
    <span className="bg-brand/10 text-brand rounded-full px-2 py-0.5 text-xs font-bold">
      {t.admin.users.active}
    </span>
  );
}
