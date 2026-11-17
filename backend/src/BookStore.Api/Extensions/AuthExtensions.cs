using System.Security.Claims;
using System.Threading.RateLimiting;
using BookStore.Application.Features.Auth;
using Microsoft.AspNetCore.RateLimiting;

namespace BookStore.Api.Extensions;

public static class Policies
{
    public const string AdminOnly = "AdminOnly";

    /// <summary>Rate-limit policy for login/register/refresh (RateLimiting:AuthPermitsPerMinute per IP, default 10).</summary>
    public const string AuthRateLimit = "auth";
}

public static class AuthExtensions
{
    public static IServiceCollection AddApiAuthorization(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddAuthorizationBuilder()
            .AddPolicy(Policies.AdminOnly, p => p.RequireAuthenticatedUser().RequireRole(Roles.Admin));

        // Partitioned by client IP (X-Forwarded-For from the Next.js server is honoured via UseForwardedHeaders).
        var permitLimit = configuration.GetValue("RateLimiting:AuthPermitsPerMinute", 10);
        services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
            options.AddPolicy(Policies.AuthRateLimit, http => RateLimitPartition.GetFixedWindowLimiter(
                http.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                _ => new FixedWindowRateLimiterOptions { PermitLimit = permitLimit, Window = TimeSpan.FromMinutes(1) }));
        });

        return services;
    }

    /// <summary>The authenticated user's id (the "sub" claim).</summary>
    public static Guid GetUserId(this ClaimsPrincipal user) =>
        Guid.TryParse(user.FindFirstValue("sub"), out var id)
            ? id
            : throw new InvalidOperationException("Authenticated principal has no valid 'sub' claim.");
}
