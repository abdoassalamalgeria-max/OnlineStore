import { NextResponse, type NextRequest } from "next/server";
import { defaultLocale, isLocale, localeCookie, type Locale } from "@/i18n/config";
import {
  accessCookie,
  cookieOptions,
  decodeToken,
  refreshCookie,
  type AuthTokens,
} from "@/lib/auth/tokens";

/**
 * Runs before every page request (and Server Action POST):
 * 1. Locale: URLs without /ar|/fr|/en are redirected (cookie → Accept-Language → Arabic).
 * 2. Session: if the access token is missing or about to expire but a refresh token exists, it is
 *    renewed here — the only place besides Server Actions where cookies can be written — and the
 *    new token is passed on to the page being rendered.
 */
export async function proxy(request: NextRequest) {
  const { pathname } = request.nextUrl;
  const first = pathname.split("/")[1];
  if (!isLocale(first)) {
    const url = request.nextUrl.clone();
    url.pathname = `/${preferredLocale(request)}${pathname === "/" ? "" : pathname}`;
    return NextResponse.redirect(url);
  }

  return refreshSessionIfNeeded(request);
}

const refreshMarginSeconds = 60;

async function refreshSessionIfNeeded(request: NextRequest) {
  const refreshToken = request.cookies.get(refreshCookie)?.value;
  if (!refreshToken) {
    return NextResponse.next();
  }

  const accessToken = request.cookies.get(accessCookie)?.value;
  const exp = accessToken ? decodeToken(accessToken)?.exp : undefined;
  if (exp && exp - Date.now() / 1000 > refreshMarginSeconds) {
    return NextResponse.next();
  }

  const tokens = await refreshOnce(refreshToken, request.headers.get("x-forwarded-for"));
  if (tokens === "retry-later") {
    return NextResponse.next(); // API unreachable: keep cookies, try on the next request
  }

  if (tokens === null) {
    // Refresh token rejected (expired, revoked, reused): end the session.
    request.cookies.delete(accessCookie);
    request.cookies.delete(refreshCookie);
    const response = NextResponse.next({ request: { headers: withCookies(request) } });
    response.cookies.delete(accessCookie);
    response.cookies.delete(refreshCookie);
    return response;
  }

  // Make the new tokens visible to this request's Server Components, and store them in the browser.
  request.cookies.set(accessCookie, tokens.accessToken);
  request.cookies.set(refreshCookie, tokens.refreshToken);
  const response = NextResponse.next({ request: { headers: withCookies(request) } });
  response.cookies.set(
    accessCookie,
    tokens.accessToken,
    cookieOptions(tokens.accessTokenExpiresAt),
  );
  response.cookies.set(
    refreshCookie,
    tokens.refreshToken,
    cookieOptions(tokens.refreshTokenExpiresAt),
  );
  return response;
}

function withCookies(request: NextRequest) {
  const headers = new Headers(request.headers);
  headers.set("cookie", request.cookies.toString());
  return headers;
}

// Parallel requests from one browser (prefetches) share a single refresh call instead of each
// rotating the same token.
const inFlight = new Map<string, Promise<AuthTokens | null | "retry-later">>();

function refreshOnce(refreshToken: string, forwardedFor: string | null) {
  let pending = inFlight.get(refreshToken);
  if (!pending) {
    pending = callRefresh(refreshToken, forwardedFor).finally(() => {
      setTimeout(() => inFlight.delete(refreshToken), 5_000);
    });
    inFlight.set(refreshToken, pending);
  }
  return pending;
}

async function callRefresh(refreshToken: string, forwardedFor: string | null) {
  try {
    const response = await fetch(
      `${process.env.API_URL ?? "http://localhost:5080"}/api/v1/auth/refresh`,
      {
        method: "POST",
        headers: {
          "Content-Type": "application/json",
          ...(forwardedFor ? { "X-Forwarded-For": forwardedFor } : {}),
        },
        body: JSON.stringify({ refreshToken }),
        cache: "no-store",
      },
    );
    if (response.status === 401 || response.status === 400) {
      return null;
    }
    if (!response.ok) {
      return "retry-later" as const;
    }
    return (await response.json()) as AuthTokens;
  } catch {
    return "retry-later" as const;
  }
}

function preferredLocale(request: NextRequest): Locale {
  const saved = request.cookies.get(localeCookie)?.value;
  if (isLocale(saved)) {
    return saved;
  }

  // "fr-FR,fr;q=0.9,ar;q=0.8" -> best supported language by q value.
  const header = request.headers.get("accept-language") ?? "";
  const ranked = header
    .split(",")
    .map((part) => {
      const [tag, ...params] = part.trim().split(";");
      const q = Number(params.find((p) => p.trim().startsWith("q="))?.split("=")[1] ?? 1);
      return { lang: tag.toLowerCase().split("-")[0], q: Number.isFinite(q) ? q : 0 };
    })
    .filter((entry) => entry.lang)
    .sort((a, b) => b.q - a.q);

  return ranked.map((entry) => entry.lang).find(isLocale) ?? defaultLocale;
}

export const config = {
  // Skip Next internals, route handlers under /api, and files with an extension (favicon.ico, images...).
  matcher: ["/((?!_next|api|.*\\..*).*)"],
};
