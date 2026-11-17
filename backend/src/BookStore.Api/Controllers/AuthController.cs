using BookStore.Api.Extensions;
using BookStore.Application.Features.Auth;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace BookStore.Api.Controllers;

/// <summary>
/// Token endpoints. Called by the Next.js server (BFF), which keeps both tokens in httpOnly cookies.
/// </summary>
[ApiController]
[Route("api/v1/auth")]
[Produces("application/json")]
public sealed class AuthController(IAuthService auth) : ControllerBase
{
    /// <summary>Creates a Customer account and signs it in.</summary>
    [HttpPost("register")]
    [EnableRateLimiting(Policies.AuthRateLimit)]
    [ProducesResponseType<AuthResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<AuthResponse>> Register(RegisterRequest request, CancellationToken cancellationToken)
    {
        var response = await auth.RegisterAsync(request, cancellationToken);
        return CreatedAtAction(nameof(Me), response);
    }

    [HttpPost("login")]
    [EnableRateLimiting(Policies.AuthRateLimit)]
    [ProducesResponseType<AuthResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    public async Task<AuthResponse> Login(LoginRequest request, CancellationToken cancellationToken) =>
        await auth.LoginAsync(request, cancellationToken);

    /// <summary>Exchanges a refresh token for a new pair; the old refresh token stops working.</summary>
    [HttpPost("refresh")]
    [EnableRateLimiting(Policies.AuthRateLimit)]
    [ProducesResponseType<AuthResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    public async Task<AuthResponse> Refresh(RefreshRequest request, CancellationToken cancellationToken) =>
        await auth.RefreshAsync(request, cancellationToken);

    [HttpPost("logout")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Logout(RefreshRequest request, CancellationToken cancellationToken)
    {
        await auth.LogoutAsync(request, cancellationToken);
        return NoContent();
    }

    [HttpGet("me")]
    [Authorize]
    [ProducesResponseType<UserDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    public async Task<UserDto> Me(CancellationToken cancellationToken) =>
        await auth.GetUserAsync(User.GetUserId(), cancellationToken);
}
