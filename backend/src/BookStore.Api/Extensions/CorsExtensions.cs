namespace BookStore.Api.Extensions;

public static class CorsExtensions
{
    public const string FrontendPolicy = "frontend";

    /// <summary>Allows only the origins listed in Cors:AllowedOrigins (the Next.js app).</summary>
    public static IServiceCollection AddFrontendCors(this IServiceCollection services, IConfiguration configuration)
    {
        var origins = configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [];

        services.AddCors(options => options.AddPolicy(FrontendPolicy, policy => policy
            .WithOrigins(origins)
            .AllowAnyHeader()
            .AllowAnyMethod()
            .AllowCredentials()));

        return services;
    }
}
