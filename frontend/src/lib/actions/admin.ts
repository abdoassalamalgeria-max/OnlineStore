"use server";

import { refresh, updateTag } from "next/cache";
import { redirect } from "next/navigation";
import { toLocale } from "@/i18n/messages";
import { getAccessToken, getSessionUser } from "@/lib/auth/session";
import { apiWithToken, fieldErrorsFrom, text, type FormState } from "./shared";

/**
 * Admin Server Actions. The API enforces the Admin role on every endpoint; checking it here too
 * means a non-admin never even reaches the API through these actions.
 */
export type AdminState = (FormState & { ok?: boolean }) | null;

async function adminApi(formData: FormData) {
  const user = await getSessionUser();
  const token = await getAccessToken();
  if (!user || !token || !user.roles.includes("Admin")) {
    redirect(`/${toLocale(formData.get("locale"))}/login`);
  }
  return { api: await apiWithToken(token), token };
}

/** Public pages cache catalog data ("use cache"); expire it so changes show immediately. */
function expireCatalog(...slugs: (string | undefined)[]) {
  updateTag("catalog");
  for (const slug of slugs) {
    if (slug) {
      updateTag(`book:${slug}`);
      updateTag(`reviews:${slug}`);
    }
  }
}

const int = (formData: FormData, name: string) => {
  const raw = text(formData, name);
  return raw === "" ? null : Number(raw);
};

function failure(status: number, error: unknown): AdminState {
  if (status === 400) return { error: "fixErrors", fieldErrors: fieldErrorsFrom(error) };
  if (status === 409) return { error: "deleteBlocked" };
  return { error: "actionFailed" };
}

// ---------------- Books ----------------

export async function saveBook(_prev: AdminState, formData: FormData): Promise<AdminState> {
  const { api } = await adminApi(formData);
  const locale = toLocale(formData.get("locale"));
  const id = text(formData, "id");
  const previousSlug = text(formData, "previousSlug");
  const body = {
    title: text(formData, "title"),
    slug: text(formData, "slug").toLowerCase(),
    description: text(formData, "description"),
    isbn: text(formData, "isbn"),
    price: int(formData, "price") ?? 0,
    salePrice: int(formData, "salePrice"),
    stock: int(formData, "stock") ?? 0,
    pageCount: int(formData, "pageCount") ?? 0,
    language: text(formData, "language"),
    publishedOn: text(formData, "publishedOn") || null,
    publisher: text(formData, "publisher"),
    isFeatured: formData.get("isFeatured") === "on",
    authorIds: formData.getAll("authorIds").map(String),
    categoryIds: formData.getAll("categoryIds").map(String),
  };

  if (id) {
    const { data, error, response } = await api.PUT("/api/v1/admin/books/{id}", {
      params: { path: { id } },
      body,
    });
    if (!data) return failure(response.status, error);
    expireCatalog(previousSlug, data.slug);
    refresh();
    return { ok: true };
  }

  const { data, error, response } = await api.POST("/api/v1/admin/books", { body });
  if (!data) return failure(response.status, error);
  expireCatalog(data.slug);
  redirect(`/${locale}/admin/books/${data.id}`);
}

export async function deleteBook(_prev: AdminState, formData: FormData): Promise<AdminState> {
  const { api } = await adminApi(formData);
  const { response, error } = await api.DELETE("/api/v1/admin/books/{id}", {
    params: { path: { id: text(formData, "id") } },
  });
  if (!response.ok) return failure(response.status, error);
  expireCatalog(text(formData, "slug"));
  redirect(`/${toLocale(formData.get("locale"))}/admin/books`);
}

/** Forwards the file to the API as multipart (openapi-fetch is JSON-oriented, so plain fetch here). */
export async function uploadCover(_prev: AdminState, formData: FormData): Promise<AdminState> {
  const { token } = await adminApi(formData);
  const file = formData.get("file");
  if (!(file instanceof File) || file.size === 0) {
    return { error: "fixErrors", fieldErrors: { file: ["—"] } };
  }

  const body = new FormData();
  body.append("file", file, file.name);
  const response = await fetch(
    `${process.env.API_URL ?? "http://localhost:5080"}/api/v1/admin/books/${encodeURIComponent(text(formData, "id"))}/cover`,
    { method: "PUT", headers: { Authorization: `Bearer ${token}` }, body, cache: "no-store" },
  );
  if (!response.ok) {
    return failure(response.status, await response.json().catch(() => undefined));
  }

  expireCatalog(text(formData, "slug"));
  refresh();
  return { ok: true };
}

export async function removeCover(formData: FormData) {
  const { api } = await adminApi(formData);
  await api.DELETE("/api/v1/admin/books/{id}/cover", {
    params: { path: { id: text(formData, "id") } },
  });
  expireCatalog(text(formData, "slug"));
  refresh();
}

// ---------------- Authors & categories ----------------

export async function saveAuthor(_prev: AdminState, formData: FormData): Promise<AdminState> {
  const { api } = await adminApi(formData);
  const id = text(formData, "id");
  const body = {
    name: text(formData, "name"),
    slug: text(formData, "slug").toLowerCase(),
    bio: text(formData, "bio") || null,
  };
  const { data, error, response } = id
    ? await api.PUT("/api/v1/admin/authors/{id}", { params: { path: { id } }, body })
    : await api.POST("/api/v1/admin/authors", { body });
  if (!data) return failure(response.status, error);
  expireCatalog();
  refresh();
  return { ok: true };
}

export async function deleteAuthor(_prev: AdminState, formData: FormData): Promise<AdminState> {
  const { api } = await adminApi(formData);
  const { response, error } = await api.DELETE("/api/v1/admin/authors/{id}", {
    params: { path: { id: text(formData, "id") } },
  });
  if (!response.ok) return failure(response.status, error);
  expireCatalog();
  refresh();
  return { ok: true };
}

export async function saveCategory(_prev: AdminState, formData: FormData): Promise<AdminState> {
  const { api } = await adminApi(formData);
  const id = text(formData, "id");
  const body = {
    nameAr: text(formData, "nameAr"),
    nameFr: text(formData, "nameFr"),
    nameEn: text(formData, "nameEn"),
    slug: text(formData, "slug").toLowerCase(),
  };
  const { data, error, response } = id
    ? await api.PUT("/api/v1/admin/categories/{id}", { params: { path: { id } }, body })
    : await api.POST("/api/v1/admin/categories", { body });
  if (!data) return failure(response.status, error);
  expireCatalog();
  refresh();
  return { ok: true };
}

export async function deleteCategory(_prev: AdminState, formData: FormData): Promise<AdminState> {
  const { api } = await adminApi(formData);
  const { response, error } = await api.DELETE("/api/v1/admin/categories/{id}", {
    params: { path: { id: text(formData, "id") } },
  });
  if (!response.ok) return failure(response.status, error);
  expireCatalog();
  refresh();
  return { ok: true };
}

// ---------------- Wilayas, orders, reviews ----------------

export async function updateWilaya(_prev: AdminState, formData: FormData): Promise<AdminState> {
  const { api } = await adminApi(formData);
  const { data, error, response } = await api.PUT("/api/v1/admin/wilayas/{code}", {
    params: { path: { code: Number(text(formData, "code")) } },
    body: {
      shippingFee: int(formData, "shippingFee") ?? 0,
      isDeliveryAvailable: formData.get("isDeliveryAvailable") === "on",
    },
  });
  if (!data) return failure(response.status, error);
  refresh();
  return { ok: true };
}

export async function orderAction(_prev: AdminState, formData: FormData): Promise<AdminState> {
  const { api } = await adminApi(formData);
  const action = text(formData, "action") as "Ship" | "Deliver" | "Cancel" | "ResolveAttention";
  const { data, error, response } = await api.POST("/api/v1/admin/orders/{id}/actions", {
    params: { path: { id: text(formData, "id") } },
    body: { action, note: text(formData, "note") || null },
  });
  if (!data) {
    return response.status === 409
      ? {
          error: "conflict",
          fieldErrors: { action: [String((error as { detail?: string })?.detail ?? "")] },
        }
      : failure(response.status, error);
  }
  // Cancelling gives stock back, which the catalog shows.
  if (action === "Cancel" || action === "ResolveAttention") expireCatalog();
  refresh();
  return { ok: true };
}

// ---------------- Users ----------------

export async function updateUser(_prev: AdminState, formData: FormData): Promise<AdminState> {
  const { api } = await adminApi(formData);
  const { data, error, response } = await api.PUT("/api/v1/admin/users/{id}", {
    params: { path: { id: text(formData, "id") } },
    body: {
      fullName: text(formData, "fullName"),
      email: text(formData, "email"),
      phoneNumber: text(formData, "phoneNumber") || null,
    },
  });
  if (!data) return failure(response.status, error);
  refresh();
  return { ok: true };
}

/** "suspend" or "reactivate". A 409 (admin account) surfaces as the form's "blocked" message. */
export async function setUserSuspended(_prev: AdminState, formData: FormData): Promise<AdminState> {
  const { api } = await adminApi(formData);
  const id = text(formData, "id");
  const { data, error, response } =
    text(formData, "action") === "suspend"
      ? await api.POST("/api/v1/admin/users/{id}/suspend", { params: { path: { id } } })
      : await api.POST("/api/v1/admin/users/{id}/reactivate", { params: { path: { id } } });
  if (!data) return failure(response.status, error);
  refresh();
  return { ok: true };
}

export async function deleteReviewAsAdmin(formData: FormData) {
  const { api } = await adminApi(formData);
  await api.DELETE("/api/v1/admin/reviews/{id}", {
    params: { path: { id: text(formData, "id") } },
  });
  expireCatalog(text(formData, "slug"));
  refresh();
}
