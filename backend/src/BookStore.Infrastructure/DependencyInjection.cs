using BookStore.Application.Abstractions;
using BookStore.Infrastructure.Identity;
using BookStore.Infrastructure.Payments;
using BookStore.Infrastructure.Persistence;
using BookStore.Infrastructure.Persistence.Seed;
using BookStore.Infrastructure.Storage;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;

namespace BookStore.Infrastructure;

public static class DependencyInjection
{
    /// <summary>Name of the health check that pings PostgreSQL; tagged "ready".</summary>
    public const string DatabaseHealthCheck = "database";

    /// <summary>
    /// Registers everything Infrastructure provides. Called once from the Api's Program.cs.
    /// </summary>
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        // Comes from user-secrets (dev), env var ConnectionStrings__Default (docker/prod). Never from appsettings.json.
        var connectionString = configuration.GetConnectionString("Default");
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException(
                "ConnectionStrings:Default is not set. See README: 'dotnet user-secrets set ConnectionStrings:Default ...'.");
        }

        // Session pooler only, TLS for Supabase, small pool (the free plan caps pooler connections).
        var normalized = DatabaseConnection.Build(connectionString, configuration.GetValue<int?>("Database:MaxPoolSize"));

        services.AddSingleton(TimeProvider.System);
        services.AddSingleton<AuditableEntityInterceptor>();

        services.AddDbContext<AppDbContext>((sp, options) =>
        {
            ConfigureDbContext(options, normalized);
            options.AddInterceptors(sp.GetRequiredService<AuditableEntityInterceptor>());
        });
        services.AddScoped<IAppDbContext>(sp => sp.GetRequiredService<AppDbContext>());
        services.AddScoped<DatabaseInitializer>();
        services.AddSingleton<IBookTextSearch, PostgresBookTextSearch>();

        services.AddIdentityAndJwt(configuration);
        services.AddPayments(configuration);
        services.AddStorage(configuration);

        services.AddHealthChecks()
            .AddCheck<PostgresHealthCheck>(DatabaseHealthCheck, tags: ["ready"]);

        return services;
    }

    /// <summary>Local uploads folder to serve at /uploads, or null when files live in Supabase.</summary>
    public static string? GetLocalUploadsRoot(IServiceProvider services) => StorageSetup.LocalUploadsRoot(services);

    /// <summary>Shared by runtime registration and the design-time factory so migrations match the app.</summary>
    internal static void ConfigureDbContext(DbContextOptionsBuilder options, string connectionString) =>
        options
            .UseNpgsql(connectionString, npgsql => npgsql
                .MigrationsHistoryTable("__ef_migrations_history")
                .EnableRetryOnFailure(maxRetryCount: 3))
            .UseSnakeCaseNamingConvention();
}
