using System.Net;
using System.Reflection;
using System.Text.Json.Serialization;
using BookStore.Api.Extensions;
using BookStore.Application;
using BookStore.Infrastructure;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.Extensions.FileProviders;

var builder = WebApplication.CreateBuilder(args);

// `dotnet build -p:OpenApiGenerateDocuments=true` runs this file inside the GetDocument tool to
// write openapi.json. It never serves requests or touches the DB, but service registration still
// needs these values, so placeholders are supplied for that mode only.
if (Assembly.GetEntryAssembly()?.GetName().Name == "GetDocument.Insider")
{
    builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
    {
        ["ConnectionStrings:Default"] = "Host=localhost;Database=openapi_generation",
        ["Jwt:SigningKey"] = "openapi-generation-placeholder-key-not-used",
    });
}

// ---- Services ----
builder.Services.AddControllers()
    .AddJsonOptions(o =>
    {
        // Enums travel as strings ("PriceAsc"), both in JSON and in the OpenAPI schema.
        o.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
        // Numbers are numbers: without this the web defaults also accept "123", and the OpenAPI
        // schema (and the generated TypeScript types) become `number | string`.
        o.JsonSerializerOptions.NumberHandling = JsonNumberHandling.Strict;
    });
// The OpenAPI generator reads the minimal-API JSON options, so mirror the settings there.
builder.Services.ConfigureHttpJsonOptions(o =>
{
    o.SerializerOptions.Converters.Add(new JsonStringEnumConverter());
    o.SerializerOptions.NumberHandling = JsonNumberHandling.Strict;
});
builder.Services.AddOpenApi("v1", o => o.AddDocumentTransformer<BearerSecuritySchemeTransformer>());

// RFC 7807 ProblemDetails for every error (exceptions, 401/403/404, validation).
builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<GlobalExceptionHandler>();

// The Next.js server calls the API on behalf of browsers and forwards the client IP in
// X-Forwarded-For (rate limiting is per client IP). Only senders listed in ReverseProxy:KnownProxies
// (IPs) or ReverseProxy:KnownNetworks (CIDR), plus loopback, are trusted. In docker-compose.prod.yml
// the web container has a fixed IP that is listed here.
builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
    foreach (var proxy in builder.Configuration.GetSection("ReverseProxy:KnownProxies").Get<string[]>() ?? [])
    {
        options.KnownProxies.Add(IPAddress.Parse(proxy));
    }

    foreach (var network in builder.Configuration.GetSection("ReverseProxy:KnownNetworks").Get<string[]>() ?? [])
    {
        options.KnownIPNetworks.Add(System.Net.IPNetwork.Parse(network));
    }
});

builder.Services.AddFrontendCors(builder.Configuration);
builder.Services.AddApiAuthorization(builder.Configuration);
builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);

var app = builder.Build();

// Deployment step: `dotnet BookStore.Api.dll migrate` (or `docker compose ... run --rm api migrate`)
// applies migrations, ensures roles/admin (+ demo catalog if Database:SeedDemoData) and the Supabase
// bucket, then exits without serving. Development does the same on startup (Database:MigrateOnStartup).
var seedDemoData = app.Configuration.GetValue<bool>("Database:SeedDemoData");
if (args is ["migrate", ..])
{
    await Deployment.PrepareAsync(app.Services, seedDemoData);
    return;
}

if (app.Configuration.GetValue<bool>("Database:MigrateOnStartup"))
{
    await Deployment.PrepareAsync(app.Services, seedDemoData);
}

// ---- HTTP pipeline ----
app.UseForwardedHeaders();
app.UseExceptionHandler();
app.UseStatusCodePages(); // empty 4xx/5xx responses (e.g. 401 from JwtBearer) become ProblemDetails

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi(); // /openapi/v1.json
    app.UseSwaggerUI(options =>
    {
        options.SwaggerEndpoint("/openapi/v1.json", "BookStore API v1");
        options.RoutePrefix = "swagger";
    });
}
else
{
    // TLS terminates at the hosting proxy; the API itself listens on plain HTTP (the Next.js server
    // reaches it on the private network), so there is no HTTPS redirection here.
    app.UseHsts();
}

// Local cover storage (development): serve uploaded files at /uploads with long-lived caching
// (file names change on every upload). In production covers are served by Supabase's CDN.
if (BookStore.Infrastructure.DependencyInjection.GetLocalUploadsRoot(app.Services) is { } uploadsRoot)
{
    Directory.CreateDirectory(uploadsRoot);
    app.UseStaticFiles(new StaticFileOptions
    {
        FileProvider = new PhysicalFileProvider(uploadsRoot),
        RequestPath = "/uploads",
        OnPrepareResponse = ctx =>
        {
            ctx.Context.Response.Headers.CacheControl = "public, max-age=31536000, immutable";
            ctx.Context.Response.Headers.XContentTypeOptions = "nosniff";
        },
    });
}

app.UseCors(CorsExtensions.FrontendPolicy);
app.UseAuthentication();
app.UseAuthorization();
app.UseRateLimiter();

app.MapControllers();
app.MapHealthEndpoints();

await app.RunAsync();

// Exposed so WebApplicationFactory<Program> can host the API in integration tests.
public partial class Program;
