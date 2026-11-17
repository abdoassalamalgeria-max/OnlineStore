// Guest cart in localStorage (browser only). Merged into the account cart at sign-in.

export type GuestLine = { bookId: string; quantity: number };

const key = "bs_guest_cart";
const changed = "guest-cart-changed";
const maxPerLine = 20;

export function readGuestCart(): GuestLine[] {
  try {
    const parsed: unknown = JSON.parse(localStorage.getItem(key) ?? "[]");
    return Array.isArray(parsed)
      ? parsed.filter(
          (l): l is GuestLine =>
            typeof l?.bookId === "string" && Number.isInteger(l?.quantity) && l.quantity > 0,
        )
      : [];
  } catch {
    return [];
  }
}

function write(lines: GuestLine[]) {
  try {
    localStorage.setItem(key, JSON.stringify(lines));
  } catch {
    // Storage full or blocked (private mode): the cart simply won't persist.
  }
  window.dispatchEvent(new Event(changed));
}

export function addToGuestCart(bookId: string, quantity: number) {
  const lines = readGuestCart();
  const line = lines.find((l) => l.bookId === bookId);
  if (line) {
    line.quantity = Math.min(line.quantity + quantity, maxPerLine);
  } else {
    lines.push({ bookId, quantity: Math.min(quantity, maxPerLine) });
  }
  write(lines);
}

/** 0 removes the line. */
export function setGuestQuantity(bookId: string, quantity: number) {
  write(
    readGuestCart()
      .map((l) => (l.bookId === bookId ? { ...l, quantity: Math.min(quantity, maxPerLine) } : l))
      .filter((l) => l.quantity > 0),
  );
}

export function clearGuestCart() {
  write([]);
}

export function onGuestCartChange(listener: () => void) {
  window.addEventListener(changed, listener);
  window.addEventListener("storage", listener); // other tabs
  return () => {
    window.removeEventListener(changed, listener);
    window.removeEventListener("storage", listener);
  };
}
