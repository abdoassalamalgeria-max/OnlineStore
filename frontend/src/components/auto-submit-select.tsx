"use client";

import type { ComponentProps } from "react";

/**
 * A <select> that submits its form as soon as the value changes (e.g. sort order).
 * Without JavaScript it is an ordinary select and the form's submit button still works.
 */
export function AutoSubmitSelect(props: ComponentProps<"select">) {
  return <select {...props} onChange={(event) => event.currentTarget.form?.requestSubmit()} />;
}
