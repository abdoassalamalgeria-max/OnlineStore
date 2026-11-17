using BookStore.Application.Features.Ordering;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace BookStore.Infrastructure.Payments;

/// <summary>
/// Every minute, cancels Pending orders whose stock reservation has lapsed (after asking the payment
/// provider whether they were in fact paid) and puts their stock back on sale.
/// Disable with Checkout:ExpirySweepEnabled=false.
/// </summary>
internal sealed partial class OrderExpiryService(
    IServiceScopeFactory scopes,
    IConfiguration configuration,
    ILogger<OrderExpiryService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!configuration.GetValue("Checkout:ExpirySweepEnabled", true))
        {
            return;
        }

        var interval = TimeSpan.FromSeconds(configuration.GetValue("Checkout:ExpirySweepSeconds", 60));
        using var timer = new PeriodicTimer(interval);
        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            try
            {
                await using var scope = scopes.CreateAsyncScope();
                var lifecycle = scope.ServiceProvider.GetRequiredService<IOrderLifecycleService>();
                var expired = await lifecycle.ExpireOverdueOrdersAsync(batchSize: 50, stoppingToken);
                if (expired > 0)
                {
                    LogExpired(logger, expired);
                }
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                // Never let one failed sweep (e.g. DB briefly down) stop future sweeps.
                LogSweepFailed(logger, ex);
            }
        }
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Expired {Count} unpaid order(s) and released their stock")]
    private static partial void LogExpired(ILogger logger, int count);

    [LoggerMessage(Level = LogLevel.Error, Message = "Order expiry sweep failed")]
    private static partial void LogSweepFailed(ILogger logger, Exception exception);
}
