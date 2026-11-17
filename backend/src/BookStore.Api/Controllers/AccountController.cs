using BookStore.Api.Extensions;
using BookStore.Application.Features.Account;
using BookStore.Application.Features.Auth;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BookStore.Api.Controllers;

[ApiController]
[Route("api/v1/account")]
[Authorize]
[Produces("application/json")]
public sealed class AccountController(IAccountService account) : ControllerBase
{
    [HttpPut("profile")]
    [ProducesResponseType<UserDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    public async Task<UserDto> UpdateProfile(UpdateProfileRequest request, CancellationToken cancellationToken) =>
        await account.UpdateProfileAsync(User.GetUserId(), request, cancellationToken);

    /// <summary>Changes the password and signs out other sessions; returns new tokens for this one.</summary>
    [HttpPost("password")]
    [ProducesResponseType<AuthResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    public async Task<AuthResponse> ChangePassword(ChangePasswordRequest request, CancellationToken cancellationToken) =>
        await account.ChangePasswordAsync(User.GetUserId(), request, cancellationToken);
}
