using BookStore.Application.Features.Auth;
using FluentValidation;

namespace BookStore.Application.Features.Account;

public sealed record UpdateProfileRequest(string FullName, string? PhoneNumber);

public sealed record ChangePasswordRequest(string CurrentPassword, string NewPassword);

public interface IAccountService
{
    Task<UserDto> UpdateProfileAsync(Guid userId, UpdateProfileRequest request, CancellationToken cancellationToken);

    /// <summary>
    /// Changes the password, signs out every other session (all refresh tokens revoked) and returns a
    /// fresh token pair so the current session continues.
    /// </summary>
    Task<AuthResponse> ChangePasswordAsync(Guid userId, ChangePasswordRequest request, CancellationToken cancellationToken);
}

public sealed class UpdateProfileRequestValidator : AbstractValidator<UpdateProfileRequest>
{
    public UpdateProfileRequestValidator()
    {
        RuleFor(r => r.FullName).NotEmpty().MaximumLength(100);
        RuleFor(r => r.PhoneNumber).Matches(PhoneRules.Pattern)
            .When(r => !string.IsNullOrWhiteSpace(r.PhoneNumber))
            .WithMessage("Enter an Algerian phone number, e.g. 0550123456 or +213550123456.");
    }
}

public sealed class ChangePasswordRequestValidator : AbstractValidator<ChangePasswordRequest>
{
    public ChangePasswordRequestValidator()
    {
        RuleFor(r => r.CurrentPassword).NotEmpty();
        RuleFor(r => r.NewPassword).NotEmpty().MinimumLength(8).MaximumLength(128)
            .Matches("[a-z]").WithMessage("Password must contain a lowercase letter.")
            .Matches("[A-Z]").WithMessage("Password must contain an uppercase letter.")
            .Matches("[0-9]").WithMessage("Password must contain a digit.")
            .NotEqual(r => r.CurrentPassword).WithMessage("The new password must differ from the current one.");
    }
}
