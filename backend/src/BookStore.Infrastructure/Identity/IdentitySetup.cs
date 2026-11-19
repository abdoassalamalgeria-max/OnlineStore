using BookStore.Application.Features.Account;
using BookStore.Application.Features.Auth;
using BookStore.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace BookStore.Infrastructure.Identity;

internal static class IdentitySetup
{
    public static IServiceCollection AddIdentityAndJwt(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<JwtOptions>()
            .Bind(configuration.GetSection(JwtOptions.Section))
            .ValidateDataAnnotations()
            .ValidateOnStart(); // fail fast if Jwt:SigningKey is missing or too short

        services.AddOptions<AdminSeedOptions>().Bind(configuration.GetSection(AdminSeedOptions.Section));

        // Identity without cookies/UI: we only need user + role management and password hashing.
        services.AddIdentityCore<ApplicationUser>(options =>
            {
                options.User.RequireUniqueEmail = true;
                options.Password.RequiredLength = 8;
                options.Password.RequireDigit = true;
                options.Password.RequireLowercase = true;
                options.Password.RequireUppercase = true;
                options.Password.RequireNonAlphanumeric = false;
                options.Lockout.MaxFailedAccessAttempts = 5;
                options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
                options.Lockout.AllowedForNewUsers = true;
            })
            .AddRoles<IdentityRole<Guid>>()
            .AddEntityFrameworkStores<AppDbContext>();

        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer();
        services.AddOptions<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme)
            .Configure<IOptions<JwtOptions>>((bearer, jwt) =>
            {
                // Keep short claim names ("sub", "role", "name") instead of the legacy SOAP URIs.
                bearer.MapInboundClaims = false;
                bearer.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidIssuer = jwt.Value.Issuer,
                    ValidAudience = jwt.Value.Audience,
                    IssuerSigningKey = TokenService.SigningKey(jwt.Value),
                    ValidateIssuer = true,
                    ValidateAudience = true,
                    ValidateIssuerSigningKey = true,
                    ValidateLifetime = true,
                    ClockSkew = TimeSpan.FromSeconds(30),
                    NameClaimType = AuthClaimTypes.Name,
                    RoleClaimType = AuthClaimTypes.Role,
                };
            });

        services.AddScoped<TokenService>();
        services.AddScoped<AuthService>();
        services.AddScoped<IAuthService>(sp => sp.GetRequiredService<AuthService>());
        services.AddScoped<IAccountService, AccountService>();
        services.AddScoped<BookStore.Application.Features.Admin.IAdminUserService, AdminUserService>();
        return services;
    }
}

/// <summary>The single admin account created at startup. Values come from user-secrets / env only.</summary>
public sealed class AdminSeedOptions
{
    public const string Section = "Seed:Admin";

    public string? Email { get; init; }
    public string? Password { get; init; }
    public string FullName { get; init; } = "مدير المتجر";
}
