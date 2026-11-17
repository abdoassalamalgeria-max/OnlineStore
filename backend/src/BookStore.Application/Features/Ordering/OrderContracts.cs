using BookStore.Application.Features.Auth;
using BookStore.Domain.Ordering;
using FluentValidation;

namespace BookStore.Application.Features.Ordering;

/// <summary>Bound from the "Checkout" section.</summary>
public sealed class CheckoutOptions
{
    public const string Section = "Checkout";

    /// <summary>Public URL of the Next.js app; Chargily sends the customer back to /{locale}/checkout/success|failure.</summary>
    public string FrontendBaseUrl { get; set; } = "http://localhost:3000";

    /// <summary>How long stock stays reserved for an unpaid order.</summary>
    public int ReservationMinutes { get; set; } = 30;
}

public sealed record ShippingAddressRequest(
    string FullName,
    string Phone,
    int WilayaCode,
    string Commune,
    string AddressLine);

/// <summary>Places an order from the signed-in user's cart. Locale selects the payment page language.</summary>
public sealed record CheckoutRequest(ShippingAddressRequest ShippingAddress, string Locale);

public sealed record CheckoutResponse(Guid OrderId, string OrderNumber, int Total, string PaymentUrl);

/// <summary>One row of "My orders".</summary>
public sealed record OrderSummaryDto(
    Guid Id,
    string OrderNumber,
    OrderStatus Status,
    int Total,
    DateTimeOffset PlacedAt,
    int ItemCount,
    IReadOnlyList<string> Titles);

public sealed record OrderItemDto(Guid BookId, string Title, int UnitPrice, int Quantity, int LineTotal);

public sealed record ShippingAddressDto(string FullName, string Phone, int WilayaCode, string WilayaName, string Commune, string AddressLine);

public sealed record OrderDto(
    Guid Id,
    string OrderNumber,
    OrderStatus Status,
    int Subtotal,
    int ShippingFee,
    int Total,
    DateTimeOffset PlacedAt,
    DateTimeOffset? PaidAt,
    DateTimeOffset ExpiresAt,
    /// <summary>Present while the order is Pending, so the customer can return to the payment page.</summary>
    string? PaymentUrl,
    ShippingAddressDto ShippingAddress,
    IReadOnlyList<OrderItemDto> Items);

public sealed class ShippingAddressRequestValidator : AbstractValidator<ShippingAddressRequest>
{
    public ShippingAddressRequestValidator()
    {
        RuleFor(a => a.FullName).NotEmpty().MaximumLength(100);
        RuleFor(a => a.Phone).NotEmpty().Matches(PhoneRules.Pattern)
            .WithMessage("Enter an Algerian phone number, e.g. 0550123456 or +213550123456.");
        RuleFor(a => a.WilayaCode).InclusiveBetween(1, 58);
        RuleFor(a => a.Commune).NotEmpty().MaximumLength(100);
        RuleFor(a => a.AddressLine).NotEmpty().MaximumLength(300);
    }
}

public sealed class CheckoutRequestValidator : AbstractValidator<CheckoutRequest>
{
    public CheckoutRequestValidator()
    {
        RuleFor(r => r.ShippingAddress).NotNull().SetValidator(new ShippingAddressRequestValidator());
        RuleFor(r => r.Locale).Must(l => l is "ar" or "fr" or "en").WithMessage("Locale must be ar, fr or en.");
    }
}
