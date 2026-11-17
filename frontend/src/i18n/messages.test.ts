import assert from "node:assert/strict";
import { readFileSync } from "node:fs";
import { test } from "node:test";

type Tree = { [key: string]: string | Tree };

const load = (locale: string): Tree =>
  JSON.parse(readFileSync(new URL(`./messages/${locale}.json`, import.meta.url), "utf8"));

/** "section.key" paths of every string, plus the {placeholders} each one uses. */
function flatten(tree: Tree, prefix = ""): Map<string, string[]> {
  const out = new Map<string, string[]>();
  for (const [key, value] of Object.entries(tree)) {
    const path = prefix ? `${prefix}.${key}` : key;
    if (typeof value === "string") {
      out.set(path, [...value.matchAll(/\{(\w+)\}/g)].map((m) => m[1]).sort());
    } else {
      for (const [p, v] of flatten(value, path)) out.set(p, v);
    }
  }
  return out;
}

const reference = flatten(load("ar"));

for (const locale of ["fr", "en"]) {
  test(`${locale}.json has exactly the keys of ar.json`, () => {
    const other = flatten(load(locale));
    const missing = [...reference.keys()].filter((k) => !other.has(k));
    const extra = [...other.keys()].filter((k) => !reference.has(k));
    assert.deepEqual({ missing, extra }, { missing: [], extra: [] });
  });

  test(`${locale}.json uses the same {placeholders} as ar.json`, () => {
    const other = flatten(load(locale));
    const mismatched = [...reference].filter(
      ([k, vars]) => other.has(k) && other.get(k)!.join() !== vars.join(),
    );
    assert.deepEqual(
      mismatched.map(([k]) => k),
      [],
    );
  });
}

test("no translation is empty", () => {
  for (const locale of ["ar", "fr", "en"]) {
    const empty = [...flattenValues(load(locale))].filter(([, v]) => v.trim() === "");
    assert.deepEqual(
      empty.map(([k]) => `${locale}:${k}`),
      [],
    );
  }
});

function* flattenValues(tree: Tree, prefix = ""): Generator<[string, string]> {
  for (const [key, value] of Object.entries(tree)) {
    const path = prefix ? `${prefix}.${key}` : key;
    if (typeof value === "string") yield [path, value];
    else yield* flattenValues(value, path);
  }
}
