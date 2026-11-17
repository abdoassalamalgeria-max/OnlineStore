using BookStore.Application.Features.Payments;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BookStore.Api.Controllers;

/// <summary>
/// Receives Chargily Pay events. Anonymous by necessity; authenticity comes from the HMAC signature
/// over the exact bytes received, which is why the body is read raw instead of model-bound.
/// </summary>
[ApiController]
[Route("api/v1/payments/webhooks")]
[AllowAnonymous]
public sealed class PaymentWebhooksController(IPaymentWebhookService webhooks) : ControllerBase
{
    /// <summary>Chargily webhook. 400 without a signature, 403 with a wrong one, otherwise 200 (also for duplicates).</summary>
    [HttpPost("chargily")]
    [RequestSizeLimit(64 * 1024)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> Chargily(CancellationToken cancellationToken)
    {
        using var reader = new StreamReader(Request.Body);
        var rawBody = await reader.ReadToEndAsync(cancellationToken);
        var outcome = await webhooks.HandleAsync(rawBody, Request.Headers["signature"].FirstOrDefault(), cancellationToken);
        return Ok(new { outcome = outcome.ToString() });
    }
}
