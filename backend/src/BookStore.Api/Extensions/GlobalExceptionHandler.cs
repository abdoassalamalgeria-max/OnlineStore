using BookStore.Application.Abstractions;
using BookStore.Application.Common;
using BookStore.Application.Features.Ordering;
using BookStore.Application.Features.Payments;
using BookStore.Domain.Common;
using FluentValidation;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace BookStore.Api.Extensions;

/// <summary>
/// Turns exceptions into RFC 7807 ProblemDetails:
///   ValidationException → 400 (with per-field errors), AuthenticationFailedException → 401,
///   NotFoundException → 404, ConflictException/DomainException → 409,
///   anything else → 500 (logged, no internals leaked).
/// </summary>
internal sealed partial class GlobalExceptionHandler(
    IProblemDetailsService problemDetailsService,
    ILogger<GlobalExceptionHandler> logger) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        ProblemDetails problem = exception switch
        {
            ValidationException validation => new ValidationProblemDetails(
                validation.Errors
                    .GroupBy(e => ToCamelCase(e.PropertyName))
                    .ToDictionary(g => g.Key, g => g.Select(e => e.ErrorMessage).ToArray()))
            {
                Status = StatusCodes.Status400BadRequest,
                Title = "One or more validation errors occurred.",
            },
            InvalidImageException image => new ValidationProblemDetails(
                new Dictionary<string, string[]> { ["file"] = [image.Message] })
            {
                Status = StatusCodes.Status400BadRequest,
                Title = "One or more validation errors occurred.",
            },
            NotFoundException notFound => new ProblemDetails
            {
                Status = StatusCodes.Status404NotFound,
                Title = "Resource not found.",
                Detail = notFound.Message,
            },
            AuthenticationFailedException auth => new ProblemDetails
            {
                Status = StatusCodes.Status401Unauthorized,
                Title = "Authentication failed.",
                Detail = auth.Message,
            },
            ForbiddenException forbidden => new ProblemDetails
            {
                Status = StatusCodes.Status403Forbidden,
                Title = "Not allowed.",
                Detail = forbidden.Message,
            },
            ConflictException conflict => new ProblemDetails
            {
                Status = StatusCodes.Status409Conflict,
                Title = "Conflict.",
                Detail = conflict.Message,
            },
            InsufficientStockException stock => new ProblemDetails
            {
                Status = StatusCodes.Status409Conflict,
                Title = "Not enough stock.",
                Detail = stock.Message,
                Extensions = { ["bookIds"] = stock.BookIds },
            },
            WebhookRejectedException webhook => new ProblemDetails
            {
                Status = webhook.StatusCode,
                Title = "Webhook rejected.",
                Detail = webhook.Message,
            },
            PaymentGatewayException => new ProblemDetails
            {
                Status = StatusCodes.Status502BadGateway,
                Title = "The payment provider is unavailable. Please try again.",
            },
            DomainException domain => new ProblemDetails
            {
                Status = StatusCodes.Status409Conflict,
                Title = "The request conflicts with a business rule.",
                Detail = domain.Message,
            },
            _ => new ProblemDetails
            {
                Status = StatusCodes.Status500InternalServerError,
                Title = "An unexpected error occurred.",
            },
        };

        if (problem.Status == StatusCodes.Status500InternalServerError)
        {
            LogUnhandled(logger, exception, httpContext.Request.Path);
        }

        httpContext.Response.StatusCode = problem.Status!.Value;
        return await problemDetailsService.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = httpContext,
            Exception = exception,
            ProblemDetails = problem,
        });
    }

    private static string ToCamelCase(string name) =>
        string.IsNullOrEmpty(name) ? name : char.ToLowerInvariant(name[0]) + name[1..];

    [LoggerMessage(Level = LogLevel.Error, Message = "Unhandled exception for {Path}")]
    private static partial void LogUnhandled(ILogger logger, Exception exception, PathString path);
}
