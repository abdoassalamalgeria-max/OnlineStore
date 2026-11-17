using BookStore.Application.Abstractions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace BookStore.Infrastructure.Storage;

internal static class StorageSetup
{
    public static IServiceCollection AddStorage(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<StorageOptions>()
            .Bind(configuration.GetSection(StorageOptions.Section))
            .Validate(o => !o.UseSupabase || (!string.IsNullOrWhiteSpace(o.SupabaseUrl) && !string.IsNullOrWhiteSpace(o.SupabaseServiceKey)),
                "Storage:Provider is Supabase but Storage:SupabaseUrl or Storage:SupabaseServiceKey is missing.")
            .ValidateOnStart();

        services.AddSingleton<IImageProcessor, SkiaImageProcessor>();

        var options = configuration.GetSection(StorageOptions.Section).Get<StorageOptions>() ?? new StorageOptions();
        if (options.UseSupabase)
        {
            services.AddHttpClient<IFileStorage, SupabaseFileStorage>(http => http.Timeout = TimeSpan.FromSeconds(30));
        }
        else
        {
            services.AddSingleton<LocalFileStorage>();
            services.AddSingleton<IFileStorage>(sp => sp.GetRequiredService<LocalFileStorage>());
        }

        return services;
    }

    /// <summary>Folder the Api must serve at /uploads when the local provider is active (null otherwise).</summary>
    public static string? LocalUploadsRoot(IServiceProvider services) =>
        services.GetRequiredService<IOptions<StorageOptions>>().Value.UseSupabase
            ? null
            : services.GetRequiredService<LocalFileStorage>().Root;
}
