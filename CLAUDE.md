# CLAUDE.md

Full-stack bookstore for the Algerian market (DZD, Chargily Pay, ar/fr/en with Arabic default + RTL).
Monorepo: `frontend/` (Next.js) and `backend/` (ASP.NET Core 10 Web API).

## Commands

Run from the repo root unless noted.

| Task | Command |
|---|---|
| Start local PostgreSQL | `docker compose up -d db` |
| Start DB + API in containers | `docker compose --profile api up -d --build` |
| Backend build | `dotnet build backend/BookStore.slnx` |
| Backend tests | `dotnet test backend/BookStore.slnx` |
| Backend format (fix / check) | `dotnet format backend/BookStore.slnx` / `... --verify-no-changes` |
| Run API (http://localhost:5080, Swagger at /swagger) | `dotnet run --project backend/src/BookStore.Api` |
| Restore local tools (dotnet-ef) | `cd backend && dotnet tool restore` |
| Add a migration | `cd backend && dotnet ef migrations add <Name> -p src/BookStore.Infrastructure -s src/BookStore.Api -o Persistence/Migrations` |
| Deployment step (migrate + roles/admin + Supabase bucket, then exit) | `dotnet run --project backend/src/BookStore.Api -- migrate` · in prod: `docker compose -f docker-compose.prod.yml --env-file .env.production run --rm api migrate` |
| Apply migrations to a given DB | `cd backend && dotnet ef database update -p src/BookStore.Infrastructure -s src/BookStore.Api --connection "<conn>"` |
| Idempotent SQL script | `cd backend && dotnet ef migrations script --idempotent -p src/BookStore.Infrastructure -s src/BookStore.Api -o migrate.sql` |
| Frontend dev (http://localhost:3000) | `cd frontend && npm run dev` |
| Frontend lint / format / types / build | `cd frontend && npm run lint` / `npm run format` / `npm run typecheck` / `npm run build` |
| Frontend unit tests (Node's built-in runner, `src/**/*.test.ts`, Node ≥ 22.18) | `cd frontend && npm test` |
| Regenerate typed API client (stop the running API first: it locks the DLLs) | `cd frontend && npm run gen:api` |
| Refresh Next route types (after adding routes) | `cd frontend && npx next typegen` |

Phase gate: after every change set, backend build + format + tests and frontend lint + format + types + tests + build
must pass. `.github/workflows/ci.yml` runs the same gate, checks the committed OpenAPI client is current, and builds
both Docker images.

Frontend tests import with explicit `.ts` extensions (`allowImportingTsExtensions`) and may only import modules whose
own imports are type-only or relative-with-extension (Node can't resolve `@/` aliases); stub browser globals in the test.

## Architecture (backend)

Clean Architecture, dependencies point inward:

```
Api ──► Application ──► Domain
 └────► Infrastructure ──► Application
```

- **Domain** (`BookStore.Domain`): entities, enums, value objects. No framework or EF references.
- **Application** (`BookStore.Application`): services per feature under `Features/<Feature>/`
  (service + DTOs + FluentValidation validators), and the interfaces Infrastructure implements
  (`IAppDbContext`, `IPaymentGateway`, `IFileStorage`, `IImageProcessor`, `ITokenService`) in `Abstractions/`.
- **Infrastructure**: EF Core + Npgsql, Identity, Chargily client, Supabase Storage, image processing.
  Registered via `AddInfrastructure()` in `DependencyInjection.cs`.
- **Api**: thin controllers under `/api/v1/...`, middleware, ProblemDetails, CORS, health checks.

## Conventions

- **Never return EF entities from controllers** — map to DTOs in Application.
- All errors are RFC 7807 ProblemDetails (`AddProblemDetails` + `GlobalExceptionHandler` + `UseStatusCodePages`).
- Validation with FluentValidation in Application; controllers do not validate by hand.
- No generic repository over EF Core. Application uses `IAppDbContext`; add a specific repository only
  for queries EF cannot express well (e.g. atomic stock updates).
- Money is `int` dinars (DZD). Chargily's `amount` is an integer in DZD; no decimals anywhere.
- IDs are `Guid` v7 (`Entity` base class); `Wilaya` is keyed by its official code (1–58).
- Database naming is snake_case (EFCore.NamingConventions). Migrations live in
  `Infrastructure/Persistence/Migrations` and are treated as generated code (no style analyzers).
- `Book.EffectivePrice` is a stored generated column (`COALESCE(sale_price, price)`); filter/sort on it.
- Catalog search goes through `IBookTextSearch` (ILIKE + pg_trgm in Infrastructure) so Application
  stays provider-agnostic. Don't call `EF.Functions.ILike` from Application.
- Reference data (wilayas) is seeded via `HasData` in the migration; the demo catalog is seeded at
  runtime by `DatabaseInitializer` when `Database:SeedDemoData` is true and the books table is empty.
- Dev startup migrates automatically (`Database:MigrateOnStartup` in appsettings.Development.json);
  production applies them explicitly with the `migrate` command (see Deployment).
- `EnableRetryOnFailure` is on: wrap explicit transactions in `db.Database.CreateExecutionStrategy()`.
- NuGet versions live only in `backend/Directory.Packages.props` (Central Package Management).
  Keep `Microsoft.*` packages on 10.0.x — 11.x targets net11.0 and will not restore.
- `Directory.Build.props` sets nullable, warnings-as-errors and code-style enforcement for all projects.
- Async all the way; pass `CancellationToken` through.
- Tests: xUnit. Unit tests for Application services; integration tests via `WebApplicationFactory<Program>`
  (`BookStoreApiFactory`), with Testcontainers PostgreSQL once the DB is involved.

## Auth

- ASP.NET Core Identity (`AddIdentityCore`, no cookies/UI) + JWT bearer. `ApplicationUser` and `RefreshToken`
  live in `Infrastructure/Identity`; other code refers to users by `Guid` only (`User.GetUserId()` reads `sub`).
- Access token: 15 min, HMAC-SHA256, claims `sub`/`email`/`name`/`role` (inbound claim mapping disabled).
- Refresh token: 256-bit random, only its SHA-256 stored; rotated on every refresh. Reusing a rotated
  token revokes its whole family. Concurrent rotations are blocked by the `xmin` row version.
- Roles: `Admin`, `Customer` (new registrations). Admin-only endpoints use `[Authorize(Policy = Policies.AdminOnly)]`.
- Lockout: 5 failed logins → 15 minutes. Login errors are generic (no account enumeration).
- Rate limit on register/login/refresh: `RateLimiting:AuthPermitsPerMinute` per client IP. The Next.js BFF must
  send `X-Forwarded-For`; production proxies go in `ReverseProxy:KnownProxies`.
- Roles and the admin (`Seed:Admin:Email`/`Password`) are ensured by `DatabaseInitializer` on startup.

## Secrets

Never commit secrets. Dev: `dotnet user-secrets` on `BookStore.Api`. Docker/prod: environment variables
(`ConnectionStrings__Default`, `Chargily__SecretKey`, `Storage__SupabaseServiceKey`, `Jwt__SigningKey`, ...).
`.env.production` (for `docker-compose.prod.yml`) is git-ignored too; `.env.production.example` lists every key.
`.env` is git-ignored; `.env.example` documents the keys.

## Frontend

- Next.js 16 App Router + React 19 + TypeScript + Tailwind v4. **Read `frontend/node_modules/next/dist/docs/`
  before using a Next.js API** — this version has breaking changes (see `frontend/AGENTS.md`).
  `cacheComponents` is enabled in `next.config.ts`.
- Server Components by default; `"use client"` only for interactive pieces (language switcher,
  auto-submit select, error boundaries).
- Prettier (with Tailwind class sorting) + ESLint (`eslint-config-next` + `eslint-config-prettier`).
- **i18n** without a library: every route lives under `app/[locale]` (root layout there, `generateStaticParams`
  = ar/fr/en). `src/proxy.ts` (Next 16's name for middleware) redirects unprefixed URLs using the
  `NEXT_LOCALE` cookie → `Accept-Language` → `ar`. Server code reads the locale with `getLocale()` /
  translations with `getDictionary()` (`src/i18n/dictionaries.ts`, built on `next/root-params`); Client
  Components get strings as props. `messages/ar.json` defines the keys; fr/en must match.
- Arabic renders `dir="rtl"`; use logical Tailwind utilities (`ms-*`, `pe-*`, `start-*`) instead of left/right.
  Book titles/authors carry `lang={book.language} dir="auto"`.
- **Data**: `src/lib/api/client.ts` = `openapi-fetch` typed by `schema.d.ts`, generated from
  `src/lib/api/openapi.json`. Regenerate both after any API change with `npm run gen:api` (builds the
  API with `-p:OpenApiGenerateDocuments=true`; no DB or running server needed). Both files are committed.
- Catalog fetchers (`src/lib/api/catalog.ts`) use `"use cache"` + `cacheLife` + `cacheTag("catalog")`.
  Anything that reads `searchParams`/`params` or calls the API without a cached input sits inside
  `<Suspense>`; home sections call `connection()` first so `next build` never needs the API.
- The API is called only from the server (`API_URL`, not `NEXT_PUBLIC_*`).
- **Session (BFF)**: tokens live in httpOnly cookies `bs_at`/`bs_rt` (`src/lib/auth`). Login/register/logout and
  cart/checkout mutations are Server Actions in `src/lib/actions` (they can't use `next/root-params`: pass the
  locale as a hidden field and use `loadDictionary`/`toLocale` from `src/i18n/messages.ts`). `proxy.ts` refreshes
  an expiring access token (deduplicated per refresh token) and forwards `X-Forwarded-For`. Per-user reads use
  `userApi()` (never inside `"use cache"`), always behind `<Suspense>`. `userApi()` calls `connection()` first:
  with `partialPrefetching`, `cookies()`/`headers()` do not end the prerendered App Shell, so an uncached fetch
  after them would run at prerender time ("encountered the unstable value `Date.now()`"). Any new per-user
  fetch outside `userApi()` must do the same.
- Guest cart: localStorage (`src/lib/guest-cart.ts`), priced via the `/api/cart/preview` route handler, merged
  into the account cart by the login/register actions.
- Errors: `app/[locale]/error.tsx` (props `{ error, retry }` in Next 16), `not-found.tsx`, `global-error.tsx`.
- Prices are formatted with `formatDzd()` ("2 500 د.ج" / "2 500 DA" / "2 500 DZD").

## Payments (Chargily Pay V2)

- Base URL test: `https://pay.chargily.net/test/api/v2`, live: `https://pay.chargily.net/api/v2`; `Authorization: Bearer <secret>`.
- Webhook: header `signature` = hex HMAC-SHA256 of the **raw body** with the API secret key; compare in constant time.
  Only the webhook marks an order Paid. Processing is idempotent.
- Stock is reserved atomically when the order is created (30-minute hold) and released on failure/expiry.
- Flow lives in Application: `CheckoutService` (cart → order + reservation in one transaction; gateway call
  outside it), `OrderLifecycleService` (webhook outcomes + expiry, idempotent via `payment_events` unique index,
  serialised by `orders.xmin`), `StockLedger` (conditional `ExecuteUpdate`, locks in book-id order).
- Webhooks match orders by Chargily **checkout id** (no reliance on `metadata`). Webhook mode (`livemode`) must
  match `Chargily:Mode`; key prefix must match mode (`ChargilyOptionsValidator`).
- `OrderExpiryService` (hosted, every 60 s) asks Chargily for the checkout status before cancelling.
- Failed/cancelled/expired orders put their books back in the customer's cart.
- Explicit transactions go through `IAppDbContext.InTransactionAsync` (retrying execution strategy;
  the change tracker is cleared per attempt, so operations must be re-runnable).
- Entity keys are client-generated (`ValueGeneratedNever` applied to every `Entity.Id`); don't remove that, or
  new children added to loaded aggregates become UPDATEs.

## Reviews & account

- `Review` (Domain/Catalog): unique (book, user), rating 1–5, `AuthorName` snapshot. `ReviewService` allows
  writing only when the user has an order in Paid/Shipped/Delivered containing the book (403 otherwise).
- Book DTOs carry `AverageRating`/`ReviewCount` (correlated subqueries on `reviews`, indexed by book_id).
- Frontend: `getReviews` is cached with tag `reviews:{slug}`; the review Server Actions call
  `updateTag("reviews:{slug}")` and `updateTag("book:{slug}")` so authors see their review immediately.
- Password change (`IAccountService`, in Infrastructure/Identity) revokes all refresh tokens and returns a fresh
  pair, which the Server Action stores, so only the current session survives.

## Admin

- Application: `AdminCatalogService` (books/authors/categories/wilayas/reviews), `AdminOrderService`
  (Ship/Deliver/Cancel/ResolveAttention; transactional, cancellation releases stock, xmin conflicts → 409),
  `AdminOverviewService`. Controllers under `/api/v1/admin/*` all use `Policies.AdminOnly`.
- Books that appear in any order can't be deleted (409; set stock 0). Authors/categories in use can't be deleted.
- Slugs are Latin `^[a-z0-9]+(-[a-z0-9]+)*$`; ISBN-10/13 check digits validated and stored without hyphens
  (`CatalogRules`).
- Covers: `IImageProcessor` (SkiaSharp, MIT) decodes (= content validation), applies the EXIF orientation, downsizes to 600 px, WebP q82,
  strips metadata; `IFileStorage` = `LocalFileStorage` (wwwroot/uploads, served at /uploads) or
  `SupabaseFileStorage` (REST, service_role key, public bucket) via `Storage:Provider`.
- Frontend admin lives in `app/[locale]/admin` (layout gate = UX only: guests → login, non-admins → 404).
  Mutations are Server Actions in `src/lib/actions/admin.ts`; they `updateTag("catalog")` (+ `book:{slug}`) so the
  public catalog updates immediately. Admin data components call `connection()` first so `next build` never
  fetches from the API. Server Action body limit is 6 MB (`next.config.ts`) for cover uploads.
- `BookCover` uses `unoptimized` (covers are pre-optimized; Next 16 blocks optimizing local-IP images).

## Deployment (Supabase)

- `Deployment.PrepareAsync` (Infrastructure) = `DatabaseInitializer` (migrations, roles, admin, optional demo catalog)
  + `SupabaseFileStorage.EnsureBucketAsync` (create or re-apply: public, `image/webp`, 2 MB). Run by
  `BookStore.Api migrate` (exits after) and by dev startup when `Database:MigrateOnStartup`.
- `DatabaseConnection.Build` normalises `ConnectionStrings:Default`: rejects Supabase's transaction pooler
  (port 6543), forces `SSL Mode=Require` for `*.supabase.co` / `*.pooler.supabase.com`, applies `Database:MaxPoolSize`, disables GSS probing (no libgssapi in the aspnet image).
  Use the **Session pooler** (IPv4, port 5432, user `postgres.<ref>`).
- Production: no Swagger, no auto-migrate, no HTTPS redirection (TLS ends at the hosting proxy). SQL command logs
  are at Warning.
- `docker-compose.prod.yml`: `api` + `web` on a fixed subnet; `web` (172.30.0.10) is the only trusted
  `X-Forwarded-For` sender (`ReverseProxy:KnownProxies`; `ReverseProxy:KnownNetworks` takes CIDRs).
- `frontend/Dockerfile` builds with `NEXT_OUTPUT=standalone` (enables `output: "standalone"` only there; local
  Windows builds stay non-standalone). Runtime env: `API_URL`, `SITE_URL`.

## Testing notes

- Integration tests: `PostgresApiFactory` uses Testcontainers, or `BOOKSTORE_TEST_DB` (connection string to any
  PostgreSQL that may CREATE DATABASE) to run without Docker. Chargily is replaced by `FakeChargily`
  (in-memory HttpMessageHandler); sign webhook bodies with `TestSettings.Sign`.
- The local docker `db` listens on **5433** (a native PostgreSQL 18 may own 5432).
- Background `next start`/`dotnet run` launched from tools can survive a task stop: free ports 3000/3100/5080
  (`Get-NetTCPConnection -LocalPort …`) before restarting.
