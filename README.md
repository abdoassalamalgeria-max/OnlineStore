# متجر الكتب — BookStore DZ

متجر إلكتروني متكامل لبيع الكتب موجّه للسوق الجزائري: الدفع بـ CIB والذهبية عبر Chargily Pay،
الأسعار بالدينار الجزائري، وواجهة بالعربية (افتراضياً) والفرنسية والإنجليزية.

| الجزء | التقنية |
|---|---|
| الواجهة الأمامية | Next.js 16 (App Router) · React 19 · TypeScript · Tailwind CSS 4 |
| الواجهة الخلفية | ASP.NET Core 10 Web API (Controllers) · EF Core · FluentValidation |
| قاعدة البيانات | PostgreSQL 17 (Docker محلياً) · Supabase (الإنتاج) |
| التخزين | Supabase Storage (صور الأغلفة) |
| المصادقة | ASP.NET Core Identity + JWT (access/refresh) |
| الدفع | Chargily Pay V2 |

> حالة المشروع: المراحل التسع مكتملة، ومجرّب على Supabase حقيقي. لم يُجرَّب بعد دفع حقيقي عبر Chargily
> في وضع الاختبار، لأنه يحتاج إلى مفتاح `test_sk_` (انظر [الدفع](#4-الدفع-عبر-chargily-pay-وضع-الاختبار)).

## الميزات

- **الكتالوج**:
  - بحث بالعنوان أو المؤلف (عربي ولاتيني).
  - تصفية حسب التصنيف والمؤلف والسعر واللغة والتوفر، وترتيب، وتقسيم إلى صفحات.
  - صفحات مُحسَّنة لمحركات البحث: بيانات وصفية لكل صفحة، و`sitemap.xml` بروابط اللغات الثلاث، و`robots.txt`.
- **السلة**: للزائر في المتصفح، وللمستخدم في قاعدة البيانات، وتُدمجان عند تسجيل الدخول.
- **الطلب والدفع**:
  - التوصيل حسب الولايات الـ 58، بسعر لكل ولاية.
  - الدفع بـ CIB والذهبية عبر Chargily، مع حجز ذرّي للمخزون.
  - تأكيد الدفع بالـ webhook الموقَّع فقط.
- **الحساب**: الملف الشخصي، وتغيير كلمة المرور، وسجل الطلبات وتفاصيلها، وتقييم الكتب المشتراة (1–5).
- **لوحة المدير**:
  - إدارة الكتب والمؤلفين والتصنيفات.
  - رفع الأغلفة: تُصغَّر وتُحوَّل إلى WebP، وتُصحَّح زاوية صور الهاتف.
  - مسار الطلبات (شحن، تسليم، إلغاء)، وأسعار التوصيل، ومراقبة التقييمات، ولوحة إحصاءات.
- **اللغات**: العربية افتراضياً باتجاه RTL، والفرنسية والإنجليزية.
- **الأمان**:
  - Identity مع JWT قصير العمر، وrefresh token يتجدد مع كل استخدام ويكشف إعادة استخدامه.
  - Tokens في ملفات تعريف ارتباط httpOnly.
  - تحديد معدل طلبات الدخول، وقفل الحساب بعد 5 محاولات فاشلة.
  - ترويسات أمان، وأسرار خارج المستودع.

## المتطلبات

- [.NET SDK 10](https://dotnet.microsoft.com/download/dotnet/10.0) (المشروع مثبّت على 10.0.x عبر `backend/global.json`)
- [Node.js](https://nodejs.org/) 20.9 أو أحدث للتشغيل، و22.18 أو أحدث لتشغيل اختبارات الواجهة (`npm test`)
- [Docker Desktop](https://www.docker.com/products/docker-desktop/)

## التشغيل المحلي

### 1. قاعدة البيانات

```bash
cp .env.example .env          # ثم غيّر POSTGRES_PASSWORD
docker compose up -d db       # على المنفذ 5433 (5432 غالباً محجوز لـ PostgreSQL مثبّت على الجهاز)
```

### 2. الواجهة الخلفية

```bash
# ضع سلسلة الاتصال في user-secrets (لا تُكتب في أي ملف داخل المستودع)
dotnet user-secrets set "ConnectionStrings:Default" \
  "Host=localhost;Port=5433;Database=bookstore;Username=bookstore;Password=<كلمة المرور من .env>" \
  --project backend/src/BookStore.Api

# مفتاح توقيع JWT (32 حرفاً على الأقل) وحساب المدير
dotnet user-secrets set "Jwt:SigningKey" "<مفتاح عشوائي طويل>" --project backend/src/BookStore.Api
dotnet user-secrets set "Seed:Admin:Email" "admin@bookstore.dz" --project backend/src/BookStore.Api
dotnet user-secrets set "Seed:Admin:Password" "<كلمة مرور قوية: 8+ أحرف، كبيرة وصغيرة ورقم>" --project backend/src/BookStore.Api

dotnet run --project backend/src/BookStore.Api
```

يُنشأ حساب المدير تلقائياً عند أول تشغيل. لمعرفة القيم المحفوظة: `dotnet user-secrets list --project backend/src/BookStore.Api`.

عند التشغيل في بيئة التطوير تُطبَّق الـ migrations تلقائياً، وتُضاف البيانات التجريبية (30 كتاباً،
24 مؤلفاً، 5 تصنيفات) إذا كان جدول الكتب فارغاً. الولايات الـ 58 تأتي ضمن الـ migration نفسها.

> البيانات التجريبية: العناوين والمؤلفون حقيقيون، أما الأسعار والمخزون وعدد الصفحات ودور النشر وأرقام ISBN فقيم تجريبية.

- Swagger: <http://localhost:5080/swagger>
- الكتالوج: `GET /api/v1/books?search=&category=&author=&language=&minPrice=&maxPrice=&featured=&inStock=&sort=Newest|PriceAsc|PriceDesc|Title&page=&pageSize=`
- المصادقة: `POST /api/v1/auth/register` · `login` · `refresh` · `logout` · `GET /api/v1/auth/me`
  (في Swagger: نفّذ login ثم الصق `accessToken` في زر Authorize)
- الحساب: `PUT /api/v1/account/profile` · `POST /api/v1/account/password` (يُخرج الأجهزة الأخرى) · طلباتي: `GET /api/v1/orders`
- التقييمات: `GET /api/v1/books/{slug}/reviews` · `GET|PUT|DELETE /api/v1/books/{slug}/reviews/mine`
- لوحة المدير (دور Admin فقط): `/api/v1/admin/overview` · `books` (CRUD + `PUT|DELETE books/{id}/cover`) · `authors` · `categories`
  · `orders` (+ `POST orders/{id}/actions`: Ship / Deliver / Cancel / ResolveAttention) · `wilayas/{code}` · `reviews`
- تفاصيل كتاب: `GET /api/v1/books/{slug}` · التصنيفات: `/api/v1/categories` · المؤلفون: `/api/v1/authors` · الولايات: `/api/v1/wilayas`
- فحص الحالة: <http://localhost:5080/health/live> و <http://localhost:5080/health/ready> (يتحقق من قاعدة البيانات)

بديل: تشغيل الـ API داخل Docker مع قاعدة البيانات:

```bash
docker compose --profile api up -d --build
```

### 3. الواجهة الأمامية

```bash
cd frontend
cp .env.example .env.local    # API_URL=http://localhost:5080
npm install
npm run dev                   # http://localhost:3000 → يُحوَّل إلى /ar (أو /fr و/en حسب لغة المتصفح)
```

- الصفحات: الرئيسية `/{ar|fr|en}`، الكتالوج `/{lang}/books?q=&category=&author=&language=&minPrice=&maxPrice=&inStock=1&sort=&page=`،
  تفاصيل كتاب `/{lang}/books/{slug}`، السلة `/{lang}/cart`، إتمام الطلب `/{lang}/checkout`،
  نتيجة الدفع `/{lang}/checkout/success|failure?order=…`، الدخول `/{lang}/login` والتسجيل `/{lang}/register`،
  الحساب `/{lang}/account` (الملف الشخصي وكلمة المرور)، طلباتي `/{lang}/account/orders` وتفاصيل الطلب `/{lang}/account/orders/{id}`.
- التقييمات (1–5 مع تعليق اختياري) في صفحة الكتاب: تقييم واحد لكل مستخدم، ولا يُسمح به إلا لمن لديه طلب مدفوع
  (أو مشحون/مُسلَّم) يحتوي الكتاب.
- سلة الزائر تُحفظ في المتصفح (localStorage) وتُدمج في سلة الحساب عند تسجيل الدخول.
- الجلسة: الـ tokens في ملفات تعريف ارتباط httpOnly لا يقرؤها JavaScript، ويجدّدها الخادم تلقائياً.
- الواجهة تتصل بالـ API من الخادم فقط، لذا يجب أن يعمل الـ API لعرض الكتب. إذا كان متوقفاً تظهر رسالة خطأ مع زر إعادة المحاولة.
- بعد أي تغيير في نقاط نهاية الـ API: `npm run gen:api` لتحديث الأنواع (TypeScript) المولّدة من OpenAPI.

### 4. الدفع عبر Chargily Pay (وضع الاختبار)

الموقع يعمل بالكامل دون Chargily، لكن زر الدفع يُرجع "خدمة الدفع غير متاحة" إلى أن تُضبط المفاتيح.

1. أنشئ حساباً في [Chargily Pay](https://pay.chargily.com) وانسخ **مفتاح الاختبار السري** (`test_sk_...`) من Developers Corner.
2. احفظه في user-secrets:
   ```bash
   dotnet user-secrets set "Chargily:SecretKey" "test_sk_..." --project backend/src/BookStore.Api
   ```
   (`Chargily:Mode` هو `test` افتراضياً. في الإنتاج: `live` مع مفتاح `live_sk_...`؛ الـ API يرفض التشغيل إذا لم يطابق المفتاح الوضع.)
3. Chargily لا يستطيع إرسال الـ webhook إلى `localhost`، لذا افتح نفقاً عاماً مؤقتاً بـ [cloudflared](https://developers.cloudflare.com/cloudflare-one/connections/connect-networks/downloads/) (مجاني، بلا حساب):
   ```bash
   cloudflared tunnel --url http://localhost:5080
   # يطبع رابطاً مثل https://random-words.trycloudflare.com
   dotnet user-secrets set "Chargily:WebhookUrl" "https://random-words.trycloudflare.com/api/v1/payments/webhooks/chargily" --project backend/src/BookStore.Api
   ```
   أعد تشغيل الـ API بعد تغيير الإعدادات. الرابط يتغير في كل مرة تشغّل فيها cloudflared.
4. جرّب: أضف كتاباً إلى السلة ← سجّل الدخول ← إتمام الطلب ← تُحوَّل إلى صفحة Chargily التجريبية ← بعد الدفع تعود إلى صفحة التأكيد التي تتحدث تلقائياً عند وصول الـ webhook.

كيف يعمل الدفع:
- عند إتمام الطلب **يُحجز المخزون** فوراً (عملية ذرّية في قاعدة البيانات) لمدة 30 دقيقة، ويُنشأ checkout في Chargily.
- **الـ webhook وحده** يؤكد الدفع: يُتحقق من توقيعه (HMAC-SHA256 بالمفتاح السري)، ولا يُعالَج الحدث نفسه مرتين.
- فشل الدفع أو إلغاؤه أو انتهاء المهلة ← يُحرَّر المخزون وتعود الكتب إلى سلة الزبون.
- قبل إلغاء طلب انتهت مهلته، يُسأل Chargily عن حالته، حتى لا يُلغى طلب دُفع وضاع إشعاره.
- إذا وصل دفع بعد إلغاء الطلب ونفد المخزون، يُعلَّم الطلب "يحتاج مراجعة" ليتصرف المدير (استرداد يدوي من لوحة Chargily).

### 5. لوحة تحكم المدير

سجّل الدخول بحساب المدير (`Seed:Admin:*`) ثم افتح `/{lang}/admin` (يظهر رابطها في الأعلى للمدير فقط):

| الصفحة | ما تفعله |
|---|---|
| نظرة عامة | الطلبات حسب الحالة، بانتظار الشحن، تحتاج مراجعة، المبيعات، الكتب منخفضة المخزون |
| الكتب | بحث (عنوان/مؤلف/ISBN)، إضافة/تعديل (المؤلفون، التصنيفات، المخزون، التخفيض، "مختار")، رفع الغلاف، حذف كتاب لم يُطلب قط |
| الطلبات | تصفية حسب الحالة والبحث؛ شحن ← تسليم؛ إلغاء (يُعيد المخزون، والطلب المدفوع يُسترد يدوياً من لوحة Chargily)؛ إعادة حجز المخزون للطلبات "تحتاج مراجعة" |
| المؤلفون / التصنيفات | إضافة وتعديل وحذف (لا يُحذف ما دامت له كتب) |
| الولايات والتوصيل | سعر التوصيل لكل ولاية وإيقاف التوصيل إليها |
| التقييمات | عرض أحدث التقييمات وحذف المسيء منها |

**صور الأغلفة**: تُرفع عبر الـ API فقط (JPEG/PNG/WebP حتى 5MB)، ويُتحقق من أنها صور فعلاً بفكّ ترميزها، ثم تُصغَّر إلى 600px
وتُحفظ WebP بلا بيانات وصفية (EXIF). مكان الحفظ تحدده الإعدادات:
- `Storage:Provider=Local` (افتراضي، للتطوير): في `backend/src/BookStore.Api/wwwroot/uploads` وتُقدَّم على `http://localhost:5080/uploads/...`.
- `Storage:Provider=Supabase` (للإنتاج): في bucket عام في Supabase Storage:
  ```bash
  dotnet user-secrets set "Storage:Provider" "Supabase" --project backend/src/BookStore.Api
  dotnet user-secrets set "Storage:SupabaseUrl" "https://<project>.supabase.co" --project backend/src/BookStore.Api
  dotnet user-secrets set "Storage:SupabaseServiceKey" "<service_role key>" --project backend/src/BookStore.Api
  # Storage:SupabaseBucket افتراضياً book-covers؛ يُنشأ تلقائياً عند الترحيل (انظر قسم النشر)
  ```
  مفتاح `service_role` يبقى في الخادم فقط ولا يصل إلى المتصفح.

> التعديلات من لوحة التحكم تظهر في الموقع فوراً (تُلغى نسخ التخزين المؤقت للكتالوج). التعديل المباشر عبر الـ API يظهر خلال دقائق.

## الاختبارات

```bash
dotnet test backend/BookStore.slnx
```

اختبارات التكامل تحتاج إلى PostgreSQL:
- **مع Docker** (الافتراضي): Testcontainers يشغّل حاوية `postgres:17-alpine` مؤقتة.
- **دون Docker**: وجّهها إلى أي خادم PostgreSQL يسمح بإنشاء قواعد بيانات (مثل نسخة مثبّتة على الجهاز)؛
  تُنشأ قاعدة باسم فريد لكل مجموعة اختبارات وتُحذف بعدها:
  ```powershell
  $env:BOOKSTORE_TEST_DB = "Host=localhost;Port=5432;Username=postgres;Password=<كلمة المرور>"
  dotnet test backend/BookStore.slnx
  ```

اختبارات الواجهة الأمامية تعمل بمشغّل الاختبارات المدمج في Node، دون أي مكتبة إضافية. تغطي:
- تنسيق الأسعار.
- قراءة فلاتر الكتالوج من الرابط.
- سلة الزائر.
- تطابق مفاتيح الترجمة بين ar وfr وen.

```bash
cd frontend && npm test
```

| المجموعة | العدد | ما تغطيه |
|---|---|---|
| Application (وحدة) | 108 | قواعد الكتالوج، المصادقة، السلة والطلب، التقييمات، الإدارة |
| Infrastructure (وحدة) | 63 | Chargily (التوقيع، الحالات)، معالجة الصور وزاوية EXIF، التخزين المحلي وSupabase، قواعد سلسلة الاتصال |
| Api (تكامل، PostgreSQL حقيقي) | 81 | كل نقاط النهاية: المصادقة، الكتالوج، الدفع وwebhook، الحساب، لوحة المدير |
| الواجهة (Node) | 16 | التنسيق، الفلاتر، سلة الزائر، الترجمات |

**التكامل المستمر**: يشغّل `.github/workflows/ci.yml` كل الفحوص على GitHub مع كل push أو pull request. تشمل:
- بناء الواجهتين واختبارهما وفحص التنسيق.
- التأكد من أن الأنواع المولّدة من OpenAPI محدّثة.
- بناء صورتَي Docker.

## الأوامر المفيدة

| المهمة | الأمر |
|---|---|
| بناء الواجهة الخلفية | `dotnet build backend/BookStore.slnx` |
| اختبارات الواجهة الخلفية | `dotnet test backend/BookStore.slnx` |
| تنسيق كود C# | `dotnet format backend/BookStore.slnx` |
| فحص/تنسيق/أنواع/اختبار/بناء الواجهة الأمامية | `npm run lint` · `npm run format` · `npm run typecheck` · `npm test` · `npm run build` (داخل `frontend/`) |
| تحديث أنواع الـ API في الواجهة | `npm run gen:api` (داخل `frontend/`، والـ API متوقف) |
| خطوة النشر (migrations + المدير + bucket) | `dotnet run --project backend/src/BookStore.Api -- migrate` |

## هيكل المستودع

```
backend/
  src/BookStore.Domain/          الكيانات وقواعد العمل
  src/BookStore.Application/     الخدمات، DTOs، التحقق (FluentValidation)
  src/BookStore.Infrastructure/  EF Core، Identity، Chargily، Supabase Storage
  src/BookStore.Api/             Controllers، Middleware، Program.cs
  tests/                         اختبارات الوحدة والتكامل (xUnit)
frontend/                        تطبيق Next.js
docker-compose.yml               PostgreSQL محلياً (+ الـ API اختيارياً)
docker-compose.prod.yml          الإنتاج: الـ API + الموقع مع Supabase
.env.production.example          متغيرات الإنتاج (انسخه إلى .env.production)
.github/workflows/ci.yml         التكامل المستمر (GitHub Actions)
```

## الأسرار

لا تُرفع أي أسرار إلى المستودع. في التطوير تُحفظ في `dotnet user-secrets`، وفي Docker والإنتاج
تُمرَّر كمتغيرات بيئة (مثل `ConnectionStrings__Default`). ملف `.env` مستثنى من git، و`.env.example` يوضح المفاتيح المطلوبة.

## النشر الحالي: Render + Vercel (مجاني، مع HTTPS)

هذا ما يعمل الآن في التجربة. كلاهما ينشر تلقائياً من فرع `main` على GitHub مع كل push.

| الجزء | المنصة | الرابط |
|---|---|---|
| الموقع (Next.js) | Vercel | `https://online-store-blush-psi.vercel.app` |
| الـ API (.NET 10) | Render (Docker، خطة Free، منطقة Frankfurt) | `https://bookstore-api-x394.onrender.com` |
| قاعدة البيانات والأغلفة | Supabase (Session pooler، باريس) | — |

**Render (Web Service):**

| الحقل | القيمة |
|---|---|
| Language | Docker |
| Root Directory | `backend` |
| Dockerfile Path | `./src/BookStore.Api/Dockerfile` |
| Health Check Path | `/health/ready` |

متغيرات البيئة هي نفسها التي في `.env.production.example`، ومعها:
- `PORT=8080`: المنفذ الذي يستمع عليه الـ API داخل الصورة.
- `Chargily__WebhookUrl=https://<api>.onrender.com/api/v1/payments/webhooks/chargily`.
- `Checkout__FrontendBaseUrl` و`Cors__AllowedOrigins__0`: رابط الموقع على Vercel.

**Vercel:**
- Root Directory = `frontend`.
- متغيرات البيئة: `API_URL` (رابط Render) و`SITE_URL` (رابط الموقع، مع تفعيل Production).
- بعد أي تغيير في المتغيرات اضغط **Redeploy**.
- يُفضَّل ضبط Function Region على `fra1` (Frankfurt)، قرب الـ API.

**ملاحظات:**
- **خطة Render المجانية:** ينام الـ API بعد 15 دقيقة دون زيارات، وأول طلب بعدها يستغرق قرابة دقيقة. لإبقائه مستيقظاً، استعمل مهمة مجانية (cron-job.org مثلاً) تزور `/health/live` كل 10 دقائق.
- **لماذا لا MonsterASP:** جرّبنا الـ API عليها. HTTPS مدفوع في خطتها المجانية، وطلبات خوادم Vercel إليها كانت تُرفض بـ 404. إن استُعملت استضافة IIS، فانظر ملاحظات «Shared IIS hosting» في CLAUDE.md، وأهمها أن الـ App Pool يجب أن يكون x64.
- **قاعدة البيانات:** طُبّقت الـ migrations عليها مسبقاً بأمر `migrate` من جهاز التطوير، فلا تحتاج المنصتان إلى ذلك. أعد تنفيذه عند إضافة migration جديدة (انظر أدناه).

## النشر (Supabase + Docker)

يتكون الإنتاج من حاويتين: الـ API (`backend/src/BookStore.Api/Dockerfile`) والموقع (`frontend/Dockerfile`).
قاعدة البيانات والأغلفة في Supabase. ملف `docker-compose.prod.yml` يجمعهما، ويعمل على أي خادم فيه Docker،
أو على منصة تدعم Docker Compose (مثل Dokploy أو Coolify).

### 1. تجهيز Supabase (الخطة المجانية تكفي)

1. أنشئ مشروعاً، واختر أقرب منطقة (مثل `eu-central-1` فرانكفورت أو `eu-west-3` باريس).
2. **سلسلة الاتصال**: Project Settings ← Database ← Connection string ← **Session pooler**.
   - صيغتها: `postgresql://postgres.<ref>:<password>@aws-0-<region>.pooler.supabase.com:5432/postgres`.
     حوّلها إلى صيغة Npgsql:
     `Host=aws-0-<region>.pooler.supabase.com;Port=5432;Database=postgres;Username=postgres.<ref>;Password=<password>`.
   - لماذا Session pooler؟ الاتصال المباشر `db.<ref>.supabase.co` يعمل عبر IPv6 فقط، وأغلب الخوادم IPv4.
     أما Transaction pooler (المنفذ 6543) فلا يناسب EF Core (لا يحفظ حالة الجلسة)، لذلك **يرفضه الـ API برسالة واضحة**.
   - التشفير TLS يُفرض تلقائياً (`SSL Mode=Require`) لكل عناوين Supabase.
3. **مفاتيح Storage**: Project Settings ← API: رابط المشروع `https://<ref>.supabase.co` ومفتاح `service_role`.
   لا حاجة لإنشاء الـ bucket يدوياً: خطوة `migrate` تنشئ `book-covers` عاماً (WebP فقط، حتى 2MB) أو تعيد ضبطه.

**حجم الـ pool**: في وضع Session كل اتصال مفتوح في Npgsql يشغل مقعداً في الـ pooler، والخطة المجانية محدودة
(حوالي 15). الافتراضي `Database__MaxPoolSize=10` لنسخة واحدة من الـ API، فقلّله إن شغّلت أكثر من نسخة.

### 2. الإعدادات

```bash
cp .env.production.example .env.production   # مستثنى من git
```

املأ القيم: روابط الموقع، وسلسلة الاتصال، ومفتاح `service_role`، ومفتاح JWT، وحساب المدير، ومفتاح Chargily.
يشرح الملف كل متغير. إن كانت منصتك تطلب متغيرات البيئة في واجهتها، فأدخل المتغيرات نفسها هناك.

### 3. البناء والترحيل والتشغيل

```bash
docker compose -f docker-compose.prod.yml --env-file .env.production build
docker compose -f docker-compose.prod.yml --env-file .env.production run --rm api migrate
docker compose -f docker-compose.prod.yml --env-file .env.production up -d
```

خطوة `migrate` تُشغَّل **مع كل نشر** قبل تشغيل النسخة الجديدة، وتكرارها آمن:
- تطبّق الـ migrations المعلّقة (ومعها الولايات الـ 58).
- تنشئ الأدوار وحساب المدير إن لم يوجد.
- تُدخل الكتب التجريبية إن كان `Database__SeedDemoData=true` وجدول الكتب فارغاً.
- تنشئ bucket الأغلفة أو تعيد ضبطه.

في الإنتاج لا يرحّل الـ API عند التشغيل، ولا يعرض Swagger.

بدائل للترحيل دون Docker:
- `dotnet run --project backend/src/BookStore.Api -- migrate` مع متغيرات البيئة نفسها.
- سكربت SQL قابل للمراجعة: `cd backend && dotnet ef migrations script --idempotent -p src/BookStore.Infrastructure -s src/BookStore.Api -o migrate.sql`،
  ثم نفّذه من SQL Editor في Supabase. هذه الطريقة لا تنشئ المدير ولا الـ bucket.

### 4. الشبكة وHTTPS

ضع أمام الحاويتين وكيلاً يتولى HTTPS (Caddy أو Traefik أو Nginx، أو وكيل منصتك):
- **الموقع** (`WEB_PORT`، افتراضياً 3000) ← `https://bookstore.example.dz`
- **الـ API** (`API_PORT`، افتراضياً 8080) ← `https://api.bookstore.example.dz`. يحتاج Chargily هذا العنوان العام
  ليرسل الـ webhook إلى `/api/v1/payments/webhooks/chargily`. الموقع نفسه يصل إلى الـ API عبر الشبكة الداخلية.

الـ API يقبل `X-Forwarded-For` من حاوية الموقع فقط (عنوانها ثابت في ملف compose)، لأن تحديد معدل طلبات
تسجيل الدخول يعتمد على عنوان الزائر. إن غيّرت طريقة النشر، فاضبط `ReverseProxy__KnownProxies__0`
أو `ReverseProxy__KnownNetworks__0` (صيغة CIDR) على عنوان خادم Next.js.

### تجربة Supabase من جهازك

احفظ سلسلة Session pooler في user-secrets باسم منفصل حتى يبقى التطوير على القاعدة المحلية
(`dotnet user-secrets set "Supabase:ConnectionString" "..." --project backend/src/BookStore.Api`)، ثم مرّرها عند الحاجة:

```powershell
$sb = (dotnet user-secrets list --project backend/src/BookStore.Api | Select-String '^Supabase:ConnectionString = (.*)$').Matches[0].Groups[1].Value
$env:ConnectionStrings__Default = $sb; $env:Storage__Provider = "Supabase"
dotnet run --project backend/src/BookStore.Api -- migrate   # أو بدون migrate لتشغيل الـ API
```

### 5. بعد النشر

- `https://api.../health/ready` يجب أن يعيد 200 (يتحقق من الاتصال بقاعدة البيانات).
- سجّل الدخول بحساب المدير، وارفع غلافاً، وتأكد أن رابطه يبدأ بـ `https://<ref>.supabase.co/storage/v1/object/public/book-covers/`.
- Chargily: ضع `Chargily__WebhookUrl` على عنوان الـ API العام. للانتقال إلى الدفع الحقيقي غيّر `Chargily__Mode=live`
  ومفتاح `live_sk_...` معاً، فالـ API يرفض التشغيل إن لم يطابق المفتاح الوضع.

## حدود معروفة

- **استرداد المبالغ**: لا توجد في Chargily V2 واجهة برمجية للاسترداد. إلغاء طلب مدفوع يعيد المخزون، أما المبلغ فيُسترد يدوياً من لوحة Chargily.
- **تعديل الكتالوج مباشرة عبر الـ API** (دون لوحة المدير): يظهر في الموقع خلال دقائق، عند انتهاء التخزين المؤقت. التعديل من لوحة المدير يظهر فوراً.
- **تغيير الاسم في الملف الشخصي**: يظهر في الترويسة بعد تجديد الجلسة التلقائي، أي خلال 15 دقيقة على الأكثر.
- **التخزين المحلي للأغلفة** (`Storage:Provider=Local`): للتطوير فقط. في Docker يحتاج volume على `/app/wwwroot/uploads`، وفي الإنتاج استعمل Supabase.
