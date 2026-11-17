using BookStore.Application.Abstractions;
using BookStore.Infrastructure.Persistence.Seed;
using BookStore.Infrastructure.Storage;
using Microsoft.Extensions.DependencyInjection;

namespace BookStore.Infrastructure;

/// <summary>
/// The one-off steps a deployment needs before the API serves traffic, all idempotent:
/// apply EF migrations, ensure roles + admin (+ demo catalog when asked), and ensure the
/// Supabase Storage bucket exists with the right settings.
/// Run in production with <c>dotnet BookStore.Api.dll migrate</c>; in development on startup.
/// </summary>
public static class Deployment
{
    public static async Task PrepareAsync(IServiceProvider services, bool seedDemoData, CancellationToken cancellationToken = default)
    {
        await using var scope = services.CreateAsyncScope();

        var initializer = scope.ServiceProvider.GetRequiredService<DatabaseInitializer>();
        await initializer.InitializeAsync(seedDemoData, cancellationToken);

        if (scope.ServiceProvider.GetRequiredService<IFileStorage>() is SupabaseFileStorage supabase)
        {
            await supabase.EnsureBucketAsync(cancellationToken);
        }
    }
}
