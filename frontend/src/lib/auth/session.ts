import { cookies } from "next/headers";
import { accessCookie, cookieOptions, decodeToken, refreshCookie, type AuthTokens } from "./tokens";

/**
 * Session = two httpOnly cookies holding the API's tokens (see tokens.ts). Only this Next.js
 * server (the BFF) reads them and attaches the access token to API calls.
 */

export type SessionUser = { id: string; name: string; email: string; roles: string[] };

/** Server Actions only (cookies can only be written there or in the proxy). */
export async function saveSession(tokens: AuthTokens) {
  const store = await cookies();
  store.set(accessCookie, tokens.accessToken, cookieOptions(tokens.accessTokenExpiresAt));
  store.set(refreshCookie, tokens.refreshToken, cookieOptions(tokens.refreshTokenExpiresAt));
}

export async function clearSession() {
  const store = await cookies();
  store.delete(accessCookie);
  store.delete(refreshCookie);
}

export async function getAccessToken(): Promise<string | undefined> {
  return (await cookies()).get(accessCookie)?.value;
}

export async function getRefreshToken(): Promise<string | undefined> {
  return (await cookies()).get(refreshCookie)?.value;
}

/** The signed-in user for display purposes (name in the header, admin link), or null. */
export async function getSessionUser(): Promise<SessionUser | null> {
  const token = await getAccessToken();
  const claims = token ? decodeToken(token) : null;
  if (!claims || typeof claims.sub !== "string") {
    return null;
  }

  const role = claims.role;
  return {
    id: claims.sub,
    name: typeof claims.name === "string" ? claims.name : "",
    email: typeof claims.email === "string" ? claims.email : "",
    roles: Array.isArray(role) ? role.map(String) : typeof role === "string" ? [role] : [],
  };
}
