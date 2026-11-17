using BookStore.Application.Common;
using BookStore.Application.Features.Account;
using BookStore.Application.Features.Auth;
using FluentValidation;
using FluentValidation.Results;
using Microsoft.AspNetCore.Identity;

namespace BookStore.Infrastructure.Identity;

/// <summary>Profile and password changes for the signed-in user.</summary>
internal sealed class AccountService(
    UserManager<ApplicationUser> users,
    AuthService auth,
    IValidator<UpdateProfileRequest> profileValidator,
    IValidator<ChangePasswordRequest> passwordValidator) : IAccountService
{
    public async Task<UserDto> UpdateProfileAsync(Guid userId, UpdateProfileRequest request, CancellationToken cancellationToken)
    {
        await profileValidator.ValidateAndThrowAsync(request, cancellationToken);
        var user = await FindAsync(userId);

        user.FullName = request.FullName.Trim();
        user.PhoneNumber = string.IsNullOrWhiteSpace(request.PhoneNumber) ? null : request.PhoneNumber.Trim();
        var result = await users.UpdateAsync(user);
        if (!result.Succeeded)
        {
            throw new ValidationException(result.Errors.Select(e => new ValidationFailure("FullName", e.Description)));
        }

        // The access token still carries the old name until its next refresh (max 15 minutes).
        return await auth.GetUserAsync(userId, cancellationToken);
    }

    public async Task<AuthResponse> ChangePasswordAsync(Guid userId, ChangePasswordRequest request, CancellationToken cancellationToken)
    {
        await passwordValidator.ValidateAndThrowAsync(request, cancellationToken);
        var user = await FindAsync(userId);

        var result = await users.ChangePasswordAsync(user, request.CurrentPassword, request.NewPassword);
        if (!result.Succeeded)
        {
            var wrongPassword = result.Errors.Any(e => e.Code == nameof(IdentityErrorDescriber.PasswordMismatch));
            throw new ValidationException(result.Errors.Select(e => new ValidationFailure(
                wrongPassword ? nameof(ChangePasswordRequest.CurrentPassword) : nameof(ChangePasswordRequest.NewPassword),
                wrongPassword ? "The current password is incorrect." : e.Description)));
        }

        return await auth.RestartSessionsAsync(user, cancellationToken);
    }

    private async Task<ApplicationUser> FindAsync(Guid userId) =>
        await users.FindByIdAsync(userId.ToString()) ?? throw new NotFoundException("User", userId);
}
