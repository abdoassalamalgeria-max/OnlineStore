import assert from "node:assert/strict";
import { test } from "node:test";
import { formatDzd, formatYear } from "./format.ts";

// Intl groups with a narrow no-break space (U+202F); normalise it to compare readable strings.
const plain = (s: string) => s.replace(/[  ]/g, " ");

test("formats dinars per locale with Algerian thousand grouping", () => {
  assert.equal(plain(formatDzd(2500, "ar")), "2 500 د.ج");
  assert.equal(plain(formatDzd(2500, "fr")), "2 500 DA");
  assert.equal(plain(formatDzd(2500, "en")), "2 500 DZD");
  assert.equal(plain(formatDzd(1234567, "en")), "1 234 567 DZD");
});

test("small and zero amounts have no grouping", () => {
  assert.equal(plain(formatDzd(600, "ar")), "600 د.ج");
  assert.equal(plain(formatDzd(0, "fr")), "0 DA");
});

test("only no-break spaces inside a price, so it never wraps across lines", () => {
  // thousands: narrow no-break space (U+202F); before the currency: no-break space (U+00A0)
  assert.match(formatDzd(2500, "ar"), /^2 500 د\.ج$/);
  assert.doesNotMatch(formatDzd(1234567, "fr"), / /);
});

test("year from ISO date", () => {
  assert.equal(formatYear("1993-01-01"), "1993");
  assert.equal(formatYear(null), null);
  assert.equal(formatYear(undefined), null);
});
