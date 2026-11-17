using BookStore.Application.Features.Admin;
using BookStore.Application.Features.Cart;
using BookStore.Application.Features.Catalog;
using BookStore.Application.Features.Ordering;
using BookStore.Application.Features.Payments;
using BookStore.Application.Features.Reviews;
using FluentValidation;
using Microsoft.Extensions.DependencyInjection;

namespace BookStore.Application;

public static class DependencyInjection
{
    /// <summary>Registers Application services and every FluentValidation validator in this assembly.</summary>
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddValidatorsFromAssemblyContaining<AssemblyMarker>(ServiceLifetime.Singleton);
        services.AddScoped<ICatalogService, CatalogService>();
        services.AddScoped<IAdminOverviewService, AdminOverviewService>();
        services.AddScoped<IAdminCatalogService, AdminCatalogService>();
        services.AddScoped<IAdminOrderService, AdminOrderService>();
        services.AddScoped<ICartService, CartService>();
        services.AddScoped<StockLedger>();
        services.AddScoped<ICheckoutService, CheckoutService>();
        services.AddScoped<IOrderLifecycleService, OrderLifecycleService>();
        services.AddScoped<IOrderQueryService, OrderQueryService>();
        services.AddScoped<IPaymentWebhookService, PaymentWebhookService>();
        services.AddScoped<IReviewService, ReviewService>();
        return services;
    }
}
