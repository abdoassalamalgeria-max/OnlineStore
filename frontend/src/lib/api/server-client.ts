import createClient from "openapi-fetch";
import { headers } from "next/headers";
import { getAccessToken } from "@/lib/auth/session";
import type { paths } from "./schema";

/**
 * API client for the current visitor: attaches their access token (from the httpOnly cookie) and
 * forwards their IP so the API's per-client rate limits apply to them, not to this server.
 * Request-time only: never use inside "use cache" functions.
 */
export async function userApi() {
  const token = await getAccessToken();
  const forwardedFor = (await headers()).get("x-forwarded-for");

  const requestHeaders: Record<string, string> = {};
  if (token) requestHeaders.Authorization = `Bearer ${token}`;
  if (forwardedFor) requestHeaders["X-Forwarded-For"] = forwardedFor;

  return createClient<paths>({
    baseUrl: process.env.API_URL ?? "http://localhost:5080",
    headers: requestHeaders,
    cache: "no-store",
  });
}
