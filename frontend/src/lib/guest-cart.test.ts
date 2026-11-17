import assert from "node:assert/strict";
import { beforeEach, test } from "node:test";

// Minimal browser stand-ins: the module only touches localStorage and window events.
const store = new Map<string, string>();
let events = 0;
Object.assign(globalThis, {
  localStorage: {
    getItem: (k: string) => store.get(k) ?? null,
    setItem: (k: string, v: string) => void store.set(k, v),
  },
  window: { dispatchEvent: () => (events++, true) },
});

const { addToGuestCart, clearGuestCart, readGuestCart, setGuestQuantity } =
  await import("./guest-cart.ts");

beforeEach(() => {
  store.clear();
  events = 0;
});

test("adds, merges and caps quantities at 20 per book", () => {
  addToGuestCart("a", 2);
  addToGuestCart("a", 3);
  addToGuestCart("b", 50);

  assert.deepEqual(readGuestCart(), [
    { bookId: "a", quantity: 5 },
    { bookId: "b", quantity: 20 },
  ]);
  addToGuestCart("a", 30);
  assert.equal(readGuestCart()[0].quantity, 20);
  assert.equal(events, 4);
});

test("quantity 0 removes the line; clear empties the cart", () => {
  addToGuestCart("a", 1);
  addToGuestCart("b", 1);
  setGuestQuantity("a", 0);
  assert.deepEqual(readGuestCart(), [{ bookId: "b", quantity: 1 }]);

  clearGuestCart();
  assert.deepEqual(readGuestCart(), []);
});

test("ignores corrupted or tampered storage", () => {
  store.set("bs_guest_cart", "{not json");
  assert.deepEqual(readGuestCart(), []);

  store.set(
    "bs_guest_cart",
    JSON.stringify([
      { bookId: "ok", quantity: 2 },
      { bookId: 5, quantity: 1 },
      { bookId: "neg", quantity: -1 },
      { bookId: "frac", quantity: 1.5 },
      null,
    ]),
  );
  assert.deepEqual(readGuestCart(), [{ bookId: "ok", quantity: 2 }]);
});
