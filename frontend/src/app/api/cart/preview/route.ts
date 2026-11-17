import { api } from "@/lib/api/client";

/**
 * Prices the guest cart from browser storage. The browser never calls the API directly
 * (API_URL is server-side), so this route handler relays the request.
 */
export async function POST(request: Request) {
  let body: unknown;
  try {
    body = await request.json();
  } catch {
    return Response.json({ title: "Invalid JSON" }, { status: 400 });
  }

  const items = (body as { items?: unknown })?.items;
  if (!Array.isArray(items)) {
    return Response.json({ title: "items must be an array" }, { status: 400 });
  }

  const { data, response } = await api.POST("/api/v1/cart/preview", {
    body: { items: items as { bookId: string; quantity: number }[] },
    cache: "no-store",
  });
  return data
    ? Response.json(data)
    : Response.json({ title: "Preview failed" }, { status: response.status });
}
