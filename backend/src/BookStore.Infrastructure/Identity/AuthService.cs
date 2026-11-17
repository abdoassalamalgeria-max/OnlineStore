using BookStore.Application.Common;
using BookStore.Application.Features.Auth;
using BookStore.Infrastructure.Persistence;
using FluentValidation;
using FluentValidation.Results;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace BookStore.Infrastructure.Identity;

/// <summary>Registration, login and refresh-token rotation on top of ASP.NET Core Identity.</summary>
internal sealed partial class AuthService(
    UserManager<ApplicationUser> users,
    AppDbContext db,
    TokenService tokens,
    IOptions<JwtOptions> jwt,
    TimeProvider clock,
    IValidator<RegisterRequest> registerValidator,
    IValidator<LoginRequest> loginValidator,
    IValidator<RefreshRequest> refreshValidator,
    ILogger<AuthService> logger) : IAuthService
{
    public async Task<AuthResponse> RegisterAsync(RegisterRequest request, CancellationToken cancellationToken)
    {
        await registerValidator.ValidateAndThrowAsync(request, cancellationToken);

        var email = request.Email.Trim();
        if (await users.FindByEmailAsync(email) is not null)
        {
            throw new ConflictException("An account with this email already exists.");
        }

        var user = new ApplicationUser
        {
            UserName = email,
            Email = email,
            FullName = request.FullName.Trim(),
            PhoneNumber = string.IsNullOrWhiteSpace(request.PhoneNumber) ? null : request.PhoneNumber.Trim(),
            CreatedAt = clock.GetUtcNow(),
        };

        ThrowIfFailed(await users.CreateAsync(user, request.Password));
        ThrowIfFailed(await users.AddToRoleAsync(user, Roles.Customer));

        return await IssueTokensAsync(user, familyId: Guid.CreateVersion7(), cancellationToken);
    }

    public async Task<AuthResponse> LoginAsync(LoginRequest request, CancellationToken cancellationToken)
    {
        await loginValidator.ValidateAndThrowAsync(request, cancellationToken);

        var user = await users.FindByEmailAsync(request.Email.Trim());
        if (user is null)
        {
            throw new AuthenticationFailedException("Invalid email or password.");
        }

        // Lockout (5 failures → 15 minutes) protects against password guessing.
        if (await users.IsLockedOutAsync(user))
        {
            throw new AuthenticationFailedException("Too many failed attempts. Try again later.");
        }

        if (!await users.CheckPasswordAsync(user, request.Password))
        {
            await users.AccessFailedAsync(user);
            throw new AuthenticationFailedException("Invalid email or password.");
        }

        await users.ResetAccessFailedCountAsync(user);
        return await IssueTokensAsync(user, familyId: Guid.CreateVersion7(), cancellationToken);
    }

    public async Task<AuthResponse> RefreshAsync(RefreshRequest request, CancellationToken cancellationToken)
    {
        await refreshValidator.ValidateAndThrowAsync(request, cancellationToken);

        var now = clock.GetUtcNow();
        var hash = TokenService.Hash(request.RefreshToken);
        var stored = await db.RefreshTokens.FirstOrDefaultAsync(t => t.TokenHash == hash, cancellationToken)
            ?? throw new AuthenticationFailedException("Invalid refresh token.");

        if (stored.RevokedAt is not null)
        {
            // Just rotated by a parallel request of the same browser (e.g. several prefetches refreshing
            // at once): reject this one, but it is not theft, so keep the session alive.
            if (stored.ReplacedByTokenHash is not null && now - stored.RevokedAt < TimeSpan.FromSeconds(jwt.Value.RefreshReuseGraceSeconds))
            {
                throw new AuthenticationFailedException("Refresh token already used.");
            }

            // A rotated token came back later: someone else may hold it. Kill the whole session family.
            await RevokeFamilyAsync(stored.FamilyId, now, cancellationToken);
            LogTokenReuse(logger, stored.UserId, stored.FamilyId);
            throw new AuthenticationFailedException("Invalid refresh token.");
        }

        if (stored.ExpiresAt <= now)
        {
            throw new AuthenticationFailedException("Refresh token expired.");
        }

        var user = await users.FindByIdAsync(stored.UserId.ToString())
            ?? throw new AuthenticationFailedException("Invalid refresh token.");

        if (await users.IsLockedOutAsync(user))
        {
            throw new AuthenticationFailedException("Account is locked.");
        }

        var response = await CreateResponseAsync(user, stored.FamilyId);
        stored.RevokedAt = now;
        stored.ReplacedByTokenHash = TokenService.Hash(response.RefreshToken);
        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            // Another request rotated this token first; only one rotation may win.
            throw new AuthenticationFailedException("Invalid refresh token.");
        }

        return response;
    }

    public async Task LogoutAsync(RefreshRequest request, CancellationToken cancellationToken)
    {
        await refreshValidator.ValidateAndThrowAsync(request, cancellationToken);

        var hash = TokenService.Hash(request.RefreshToken);
        var stored = await db.RefreshTokens.FirstOrDefaultAsync(t => t.TokenHash == hash, cancellationToken);
        if (stored is { RevokedAt: null })
        {
            stored.RevokedAt = clock.GetUtcNow();
            await db.SaveChangesAsync(cancellationToken);
        }
    }

    public async Task<UserDto> GetUserAsync(Guid userId, CancellationToken cancellationToken)
    {
        var user = await users.FindByIdAsync(userId.ToString()) ?? throw new NotFoundException("User", userId);
        return ToDto(user, await users.GetRolesAsync(user));
    }

    /// <summary>Revokes every refresh token of the user and starts a new session (used after a password change).</summary>
    internal async Task<AuthResponse> RestartSessionsAsync(ApplicationUser user, CancellationToken cancellationToken)
    {
        var now = clock.GetUtcNow();
        await db.RefreshTokens
            .Where(t => t.UserId == user.Id && t.RevokedAt == null)
            .ExecuteUpdateAsync(s => s.SetProperty(t => t.RevokedAt, now), cancellationToken);
        return await IssueTokensAsync(user, familyId: Guid.CreateVersion7(), cancellationToken);
    }

    private async Task<AuthResponse> IssueTokensAsync(ApplicationUser user, Guid familyId, CancellationToken cancellationToken)
    {
        var response = await CreateResponseAsync(user, familyId);
        await db.SaveChangesAsync(cancellationToken);
        return response;
    }

    /// <summary>Builds the token pair and stages (does not save) the new refresh-token row.</summary>
    private async Task<AuthResponse> CreateResponseAsync(ApplicationUser user, Guid familyId)
    {
        var roles = await users.GetRolesAsync(user);
        var (accessToken, accessExpires) = tokens.CreateAccessToken(user, roles);
        var (refreshToken, refreshExpires) = tokens.CreateRefreshToken();

        db.RefreshTokens.Add(new RefreshToken
        {
            UserId = user.Id,
            FamilyId = familyId,
            TokenHash = TokenService.Hash(refreshToken),
            CreatedAt = clock.GetUtcNow(),
            ExpiresAt = refreshExpires,
        });

        return new AuthResponse(accessToken, accessExpires, refreshToken, refreshExpires, ToDto(user, roles));
    }

    private async Task RevokeFamilyAsync(Guid familyId, DateTimeOffset now, CancellationToken cancellationToken) =>
        await db.RefreshTokens
            .Where(t => t.FamilyId == familyId && t.RevokedAt == null)
            .ExecuteUpdateAsync(s => s.SetProperty(t => t.RevokedAt, now), cancellationToken);

    private static UserDto ToDto(ApplicationUser user, IEnumerable<string> roles) =>
        new(user.Id, user.Email ?? string.Empty, user.FullName, user.PhoneNumber, [.. roles.Order()]);

    /// <summary>Surfaces Identity errors (e.g. password policy) as a 400 validation problem.</summary>
    private static void ThrowIfFailed(IdentityResult result)
    {
        if (!result.Succeeded)
        {
            throw new ValidationException(result.Errors.Select(e =>
                new ValidationFailure(e.Code.Contains("Password", StringComparison.Ordinal) ? "Password" : "Email", e.Description)));
        }
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "Refresh token reuse detected for user {UserId}; revoked family {FamilyId}")]
    private static partial void LogTokenReuse(ILogger logger, Guid userId, Guid familyId);
}
