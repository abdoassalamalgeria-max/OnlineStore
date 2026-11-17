using FluentValidation;

namespace BookStore.Application.Features.Auth;

/// <summary>Role names. Stored in Identity and emitted as "role" claims in the access token.</summary>
public static class Roles
{
    public const string Admin = "Admin";
    public const string Customer = "Customer";

    public static readonly IReadOnlyList<string> All = [Admin, Customer];
}

public sealed record RegisterRequest(string FullName, string Email, string Password, string? PhoneNumber);

public sealed record LoginRequest(string Email, string Password);

public sealed record RefreshRequest(string RefreshToken);

public sealed record UserDto(Guid Id, string Email, string FullName, string? PhoneNumber, IReadOnlyList<string> Roles);

/// <summary>
/// Issued on register/login/refresh. The Next.js BFF stores both tokens in httpOnly cookies;
/// they never reach browser JavaScript.
/// </summary>
public sealed record AuthResponse(
    string AccessToken,
    DateTimeOffset AccessTokenExpiresAt,
    string RefreshToken,
    DateTimeOffset RefreshTokenExpiresAt,
    UserDto User);

public interface IAuthService
{
    Task<AuthResponse> RegisterAsync(RegisterRequest request, CancellationToken cancellationToken);
    Task<AuthResponse> LoginAsync(LoginRequest request, CancellationToken cancellationToken);

    /// <summary>Rotates the refresh token: the presented one is revoked and a new pair is issued.</summary>
    Task<AuthResponse> RefreshAsync(RefreshRequest request, CancellationToken cancellationToken);

    /// <summary>Revokes the refresh token (idempotent: unknown or already-revoked tokens are ignored).</summary>
    Task LogoutAsync(RefreshRequest request, CancellationToken cancellationToken);

    Task<UserDto> GetUserAsync(Guid userId, CancellationToken cancellationToken);
}

/// <summary>Algerian phone rules shared by registration and (later) the shipping address.</summary>
public static class PhoneRules
{
    /// <summary>Mobile (05/06/07) or landline (0 + 8 digits), local or +213 form.</summary>
    public const string Pattern = @"^(?:0|\+213)(?:[567]\d{8}|[2-4]\d{7})$";
}

public sealed class RegisterRequestValidator : AbstractValidator<RegisterRequest>
{
    public RegisterRequestValidator()
    {
        RuleFor(r => r.FullName).NotEmpty().MaximumLength(100);
        RuleFor(r => r.Email).NotEmpty().EmailAddress().MaximumLength(256);
        RuleFor(r => r.Password).NotEmpty().MinimumLength(8).MaximumLength(128)
            .Matches("[a-z]").WithMessage("Password must contain a lowercase letter.")
            .Matches("[A-Z]").WithMessage("Password must contain an uppercase letter.")
            .Matches("[0-9]").WithMessage("Password must contain a digit.");
        RuleFor(r => r.PhoneNumber).Matches(PhoneRules.Pattern)
            .When(r => !string.IsNullOrWhiteSpace(r.PhoneNumber))
            .WithMessage("Enter an Algerian phone number, e.g. 0550123456 or +213550123456.");
    }
}

public sealed class LoginRequestValidator : AbstractValidator<LoginRequest>
{
    public LoginRequestValidator()
    {
        RuleFor(r => r.Email).NotEmpty().EmailAddress();
        RuleFor(r => r.Password).NotEmpty();
    }
}

public sealed class RefreshRequestValidator : AbstractValidator<RefreshRequest>
{
    public RefreshRequestValidator() => RuleFor(r => r.RefreshToken).NotEmpty().MaximumLength(200);
}
