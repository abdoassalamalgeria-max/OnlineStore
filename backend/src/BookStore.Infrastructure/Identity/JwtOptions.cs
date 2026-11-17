using System.ComponentModel.DataAnnotations;

namespace BookStore.Infrastructure.Identity;

/// <summary>Bound from the "Jwt" section. SigningKey must come from user-secrets / env (Jwt__SigningKey).</summary>
public sealed class JwtOptions
{
    public const string Section = "Jwt";

    [Required]
    public string Issuer { get; init; } = "bookstore-api";

    [Required]
    public string Audience { get; init; } = "bookstore-frontend";

    /// <summary>HMAC-SHA256 key; at least 32 characters (256 bits).</summary>
    [Required]
    [MinLength(32)]
    public string SigningKey { get; init; } = string.Empty;

    [Range(1, 120)]
    public int AccessTokenMinutes { get; init; } = 15;

    [Range(1, 90)]
    public int RefreshTokenDays { get; init; } = 14;

    /// <summary>
    /// A rotated refresh token presented again within this many seconds is treated as a benign
    /// parallel refresh (rejected, session kept); later reuse revokes the whole token family.
    /// </summary>
    [Range(0, 120)]
    public int RefreshReuseGraceSeconds { get; init; } = 20;
}
