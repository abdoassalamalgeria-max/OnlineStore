using BookStore.Application.Abstractions;
using BookStore.Application.Features.Ordering;
using BookStore.Infrastructure.Payments.Chargily;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace BookStore.Infrastructure.Payments;

internal static class PaymentsSetup
{
    public static IServiceCollection AddPayments(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<CheckoutOptions>().Bind(configuration.GetSection(CheckoutOptions.Section));

        services.AddOptions<ChargilyOptions>()
            .Bind(configuration.GetSection(ChargilyOptions.Section))
            .ValidateOnStart();
        services.AddSingleton<IValidateOptions<ChargilyOptions>, ChargilyOptionsValidator>();

        services.AddHttpClient<IPaymentGateway, ChargilyPaymentGateway>((sp, http) =>
        {
            http.BaseAddress = new Uri(sp.GetRequiredService<IOptions<ChargilyOptions>>().Value.BaseUrl);
            http.Timeout = TimeSpan.FromSeconds(15);
        });

        services.AddHostedService<OrderExpiryService>();
        return services;
    }
}
