// Cookie names and token helpers shared by proxy.ts, Server Actions and Server Components.
// No next/headers import here, so the proxy can use it.

export const accessCookie = "bs_at";
export const refreshCookie = "bs_rt";

export type AuthTokens = {
  accessToken: string;
  accessTokenExpiresAt: string;
  refreshToken: string;
  refreshTokenExpiresAt: string;
};

/** httpOnly: browser JavaScript can never read the tokens. Secure in production (HTTPS only). */
export function cookieOptions(expiresAt: string) {
  return {
    httpOnly: true,
    secure: process.env.NODE_ENV === "production",
    sameSite: "lax" as const,
    path: "/",
    expires: new Date(expiresAt),
  };
}

/** Reads the JWT payload without verifying it (the API verifies every request). */
export function decodeToken(token: string): (Record<string, unknown> & { exp?: number }) | null {
  try {
    const payload = token.split(".")[1];
    return JSON.parse(Buffer.from(payload, "base64url").toString("utf8"));
  } catch {
    return null;
  }
}
