namespace BookStore.Infrastructure.Identity;

/// <summary>
/// One issued refresh token. Only its SHA-256 hash is stored, so a database leak does not leak usable tokens.
/// Tokens created by rotating each other share a <see cref="FamilyId"/>; presenting an already-rotated
/// token (reuse ⇒ likely theft) revokes the whole family.
/// </summary>
public sealed class RefreshToken
{
    public Guid Id { get; init; } = Guid.CreateVersion7();
    public Guid UserId { get; init; }
    public Guid FamilyId { get; init; }
    public required string TokenHash { get; init; }
    public DateTimeOffset CreatedAt { get; init; }
    public DateTimeOffset ExpiresAt { get; init; }
    public DateTimeOffset? RevokedAt { get; set; }

    /// <summary>Hash of the token that replaced this one during rotation (null if revoked by logout/reuse).</summary>
    public string? ReplacedByTokenHash { get; set; }

    /// <summary>PostgreSQL xmin; makes two concurrent rotations of the same token conflict instead of both succeeding.</summary>
    public uint Version { get; set; }

    public bool IsActive(DateTimeOffset now) => RevokedAt is null && ExpiresAt > now;
}
