using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace BookStore.Infrastructure.Identity;

/// <summary>Creates signed access tokens and opaque random refresh tokens.</summary>
internal sealed class TokenService(IOptions<JwtOptions> options, TimeProvider clock)
{
    private readonly JwtOptions _options = options.Value;

    public (string Token, DateTimeOffset ExpiresAt) CreateAccessToken(ApplicationUser user, IEnumerable<string> roles)
    {
        var now = clock.GetUtcNow();
        var expires = now.AddMinutes(_options.AccessTokenMinutes);

        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
            new(JwtRegisteredClaimNames.Email, user.Email ?? string.Empty),
            new(JwtRegisteredClaimNames.Name, user.FullName),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
        };
        claims.AddRange(roles.Select(r => new Claim(AuthClaimTypes.Role, r)));

        var descriptor = new SecurityTokenDescriptor
        {
            Issuer = _options.Issuer,
            Audience = _options.Audience,
            Subject = new ClaimsIdentity(claims),
            NotBefore = now.UtcDateTime,
            IssuedAt = now.UtcDateTime,
            Expires = expires.UtcDateTime,
            SigningCredentials = new SigningCredentials(SigningKey(_options), SecurityAlgorithms.HmacSha256),
        };

        return (new JsonWebTokenHandler().CreateToken(descriptor), expires);
    }

    /// <summary>A 256-bit random token (base64url) and its expiry. Only <see cref="Hash"/> is persisted.</summary>
    public (string Token, DateTimeOffset ExpiresAt) CreateRefreshToken() =>
        (Base64UrlEncoder.Encode(RandomNumberGenerator.GetBytes(32)),
         clock.GetUtcNow().AddDays(_options.RefreshTokenDays));

    public static string Hash(string refreshToken) =>
        Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(refreshToken)));

    public static SymmetricSecurityKey SigningKey(JwtOptions options) =>
        new(Encoding.UTF8.GetBytes(options.SigningKey));
}

/// <summary>Short claim names used in our tokens (inbound claim mapping is disabled).</summary>
public static class AuthClaimTypes
{
    public const string Role = "role";
    public const string Name = JwtRegisteredClaimNames.Name;
    public const string Subject = JwtRegisteredClaimNames.Sub;
}
