import { cacheLife, cacheTag } from "next/cache";
import { api, ApiError, type BookDetails, type BookSort, type PagedBooks } from "./client";

/**
 * Catalog reads. Each function is cached ("use cache") so repeated visits within a few minutes
 * don't hit the API; the arguments are part of the cache key. Admin edits (phase 7) will call
 * revalidateTag("catalog") to refresh immediately.
 */

export type BookFilters = {
  search?: string;
  category?: string;
  author?: string;
  language?: string;
  minPrice?: number;
  maxPrice?: number;
  featured?: boolean;
  inStock?: boolean;
  sort?: BookSort;
  page?: number;
  pageSize?: number;
};

export async function getBooks(filters: BookFilters): Promise<PagedBooks> {
  "use cache";
  cacheLife("minutes");
  cacheTag("catalog");

  const { data, response } = await api.GET("/api/v1/books", {
    params: {
      query: {
        Search: filters.search,
        Category: filters.category,
        Author: filters.author,
        Language: filters.language,
        MinPrice: filters.minPrice,
        MaxPrice: filters.maxPrice,
        Featured: filters.featured,
        InStock: filters.inStock,
        Sort: filters.sort,
        Page: filters.page,
        PageSize: filters.pageSize,
      },
    },
  });
  if (!data) {
    throw new ApiError(response.status, "/api/v1/books");
  }
  return data;
}

/** Null when the slug does not exist (the page then calls notFound()). */
export async function getBook(slug: string): Promise<BookDetails | null> {
  "use cache";
  cacheLife("minutes");
  cacheTag("catalog", `book:${slug}`);

  const { data, response } = await api.GET("/api/v1/books/{slug}", {
    params: { path: { slug } },
  });
  if (response.status === 404) {
    return null;
  }
  if (!data) {
    throw new ApiError(response.status, `/api/v1/books/${slug}`);
  }
  return data;
}

/** Rating summary + one page of reviews. Expired by the review Server Actions (updateTag). */
export async function getReviews(slug: string, page = 1) {
  "use cache";
  cacheLife("minutes");
  cacheTag(`reviews:${slug}`);

  const { data, response } = await api.GET("/api/v1/books/{slug}/reviews", {
    params: { path: { slug }, query: { page, pageSize: 10 } },
  });
  if (response.status === 404) {
    return null;
  }
  if (!data) {
    throw new ApiError(response.status, `/api/v1/books/${slug}/reviews`);
  }
  return data;
}

export async function getCategories() {
  "use cache";
  cacheLife("hours");
  cacheTag("catalog");

  const { data, response } = await api.GET("/api/v1/categories");
  if (!data) {
    throw new ApiError(response.status, "/api/v1/categories");
  }
  return data;
}

export async function getAuthors() {
  "use cache";
  cacheLife("hours");
  cacheTag("catalog");

  const { data, response } = await api.GET("/api/v1/authors");
  if (!data) {
    throw new ApiError(response.status, "/api/v1/authors");
  }
  return data;
}
