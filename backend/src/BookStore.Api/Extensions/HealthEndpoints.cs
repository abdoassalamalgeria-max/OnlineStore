using Microsoft.AspNetCore.Diagnostics.HealthChecks;

namespace BookStore.Api.Extensions;

public static class HealthEndpoints
{
    /// <summary>
    /// /health/live  — the process is up (no dependencies checked).
    /// /health/ready — dependencies tagged "ready" (PostgreSQL) are reachable.
    /// </summary>
    public static void MapHealthEndpoints(this WebApplication app)
    {
        app.MapHealthChecks("/health/live", new HealthCheckOptions { Predicate = _ => false });
        app.MapHealthChecks("/health/ready", new HealthCheckOptions { Predicate = check => check.Tags.Contains("ready") });
    }
}
