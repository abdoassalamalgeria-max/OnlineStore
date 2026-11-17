import type { NextConfig } from "next";

const nextConfig: NextConfig = {
  // The Docker image runs the minimal standalone server (see Dockerfile). Off for local builds:
  // on Windows the standalone copy needs symlink privileges.
  output: process.env.NEXT_OUTPUT === "standalone" ? "standalone" : undefined,
  cacheComponents: true,
  poweredByHeader: false,
  partialPrefetching: true,
  experimental: {
    serverActions: {
      // Admin cover uploads go through a Server Action (the API accepts up to 5 MB);
      // leave room for multipart overhead.
      bodySizeLimit: "6mb",
    },
  },
  // Baseline security headers on every response. HSTS only in production (TLS ends at the proxy
  // in front of this server; browsers ignore it over plain HTTP anyway).
  async headers() {
    const security = [
      { key: "X-Content-Type-Options", value: "nosniff" },
      { key: "X-Frame-Options", value: "DENY" },
      { key: "Referrer-Policy", value: "strict-origin-when-cross-origin" },
      { key: "Permissions-Policy", value: "camera=(), microphone=(), geolocation=(), payment=()" },
      ...(process.env.NODE_ENV === "production"
        ? [{ key: "Strict-Transport-Security", value: "max-age=31536000; includeSubDomains" }]
        : []),
    ];
    return [{ source: "/:path*", headers: security }];
  },
  turbopack: {
    rules: {
      "*.css": {
        loaders: ["@tailwindcss/turbopack"],
        as: "*.css",
      },
    },
  },
};

export default nextConfig;
