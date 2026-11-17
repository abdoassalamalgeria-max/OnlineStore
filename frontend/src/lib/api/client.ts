import createClient from "openapi-fetch";
import type { components, paths } from "./schema";

/**
 * Typed client for the ASP.NET Core API. `paths`/`components` are generated from the backend's
 * OpenAPI document (`npm run gen:api`), so request params and responses are checked at compile time.
 *
 * Used from the server only: API_URL has no NEXT_PUBLIC_ prefix and never reaches the browser.
 */
export const api = createClient<paths>({
  baseUrl: process.env.API_URL ?? "http://localhost:5080",
});

export type Schemas = components["schemas"];
export type BookSummary = Schemas["BookSummaryDto"];
export type BookDetails = Schemas["BookDetailsDto"];
export type Category = Schemas["CategoryDto"];
export type CategoryRef = Schemas["CategoryRefDto"];
export type Author = Schemas["AuthorDto"];
export type BookSort = Schemas["BookSort"];
export type PagedBooks = Schemas["PagedResultOfBookSummaryDto"];

/** Thrown for unexpected API responses; error.tsx boundaries render a retry UI. */
export class ApiError extends Error {
  constructor(
    readonly status: number,
    readonly path: string,
  ) {
    super(`API ${path} responded ${status}`);
    this.name = "ApiError";
  }
}
