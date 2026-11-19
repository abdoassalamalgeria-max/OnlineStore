using BookStore.Application.Common;
using BookStore.Application.Features.Auth;
using BookStore.Application.Features.Ordering;
using FluentValidation;

namespace BookStore.Application.Features.Admin;

/// <summary>Filter for the admin user list. Search matches name, email or phone.</summary>
public sealed record AdminUserQuery(string? Search, bool? Suspended, int Page = 1, int PageSize = 20)
{
    public const int MaxPageSize = 50;
}

public sealed record AdminUserListItemDto(
    Guid Id,
    string FullName,
    string Email,
    string? PhoneNumber,
    DateTimeOffset CreatedAt,
    IReadOnlyList<string> Roles,
    bool IsSuspended,
    int OrderCount);

public sealed record AdminUserDto(
    Guid Id,
    string FullName,
    string Email,
    string? PhoneNumber,
    DateTimeOffset CreatedAt,
    IReadOnlyList<string> Roles,
    bool IsSuspended,
    DateTimeOffset? LockedOutUntil, // set while a temporary failed-sign-in lockout runs; null otherwise
    int OrderCount,
    int TotalSpent, // DZD, orders that were paid (Paid, Shipped or Delivered)
    IReadOnlyList<OrderSummaryDto> RecentOrders);

/// <summary>Personal details an admin may correct. The email is also the sign-in name.</summary>
public sealed record AdminUpdateUserRequest(string FullName, string Email, string? PhoneNumber);

/// <summary>Registered customers (and admins) for the back office: list, details, edit, suspend.</summary>
public interface IAdminUserService
{
    Task<PagedResult<AdminUserListItemDto>> ListAsync(AdminUserQuery query, CancellationToken cancellationToken);

    Task<AdminUserDto> GetAsync(Guid id, CancellationToken cancellationToken);

    /// <summary>Validation errors (400) for bad input or an email already used by another account.</summary>
    Task<AdminUserDto> UpdateAsync(Guid id, AdminUpdateUserRequest request, CancellationToken cancellationToken);

    /// <summary>
    /// Suspending blocks sign-in and token refresh and ends every session (refresh tokens revoked); an
    /// access token already issued stays valid until it expires (15 minutes at most). Admin accounts can't be
    /// suspended here (409). Reactivating also clears any temporary failed-sign-in lockout.
    /// </summary>
    Task<AdminUserDto> SetSuspendedAsync(Guid id, bool suspended, CancellationToken cancellationToken);
}

public sealed class AdminUserQueryValidator : AbstractValidator<AdminUserQuery>
{
    public AdminUserQueryValidator()
    {
        RuleFor(q => q.Search).MaximumLength(100);
        RuleFor(q => q.Page).GreaterThanOrEqualTo(1);
        RuleFor(q => q.PageSize).InclusiveBetween(1, AdminUserQuery.MaxPageSize);
    }
}

public sealed class AdminUpdateUserRequestValidator : AbstractValidator<AdminUpdateUserRequest>
{
    public AdminUpdateUserRequestValidator()
    {
        RuleFor(r => r.FullName).NotEmpty().MaximumLength(100);
        RuleFor(r => r.Email).NotEmpty().EmailAddress().MaximumLength(256);
        RuleFor(r => r.PhoneNumber).Matches(PhoneRules.Pattern)
            .When(r => !string.IsNullOrWhiteSpace(r.PhoneNumber))
            .WithMessage("Enter an Algerian phone number, e.g. 0550123456 or +213550123456.");
    }
}
