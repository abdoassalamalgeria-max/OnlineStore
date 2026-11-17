using BookStore.Infrastructure.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BookStore.Infrastructure.Persistence.Configurations;

internal sealed class ApplicationUserConfiguration : IEntityTypeConfiguration<ApplicationUser>
{
    public void Configure(EntityTypeBuilder<ApplicationUser> builder)
    {
        builder.ToTable("users");
        builder.Property(u => u.FullName).HasMaxLength(100).IsRequired();
        builder.Property(u => u.PhoneNumber).HasMaxLength(20);
    }
}

internal sealed class RefreshTokenConfiguration : IEntityTypeConfiguration<RefreshToken>
{
    public void Configure(EntityTypeBuilder<RefreshToken> builder)
    {
        builder.ToTable("refresh_tokens");
        builder.Property(t => t.TokenHash).HasMaxLength(64).IsRequired();
        builder.Property(t => t.ReplacedByTokenHash).HasMaxLength(64);

        // Npgsql maps a uint row version to the system column xmin (no extra column is created).
        builder.Property(t => t.Version).IsRowVersion();

        builder.HasIndex(t => t.TokenHash).IsUnique();
        builder.HasIndex(t => t.FamilyId);
        builder.HasIndex(t => t.UserId);

        builder.HasOne<ApplicationUser>()
            .WithMany()
            .HasForeignKey(t => t.UserId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

/// <summary>Short, snake_case names instead of the default AspNet* tables.</summary>
internal static class IdentityTableNames
{
    public static void Apply(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<IdentityRole<Guid>>().ToTable("roles");
        modelBuilder.Entity<IdentityUserRole<Guid>>().ToTable("user_roles");
        modelBuilder.Entity<IdentityUserClaim<Guid>>().ToTable("user_claims");
        modelBuilder.Entity<IdentityUserLogin<Guid>>().ToTable("user_logins");
        modelBuilder.Entity<IdentityUserToken<Guid>>().ToTable("user_tokens");
        modelBuilder.Entity<IdentityRoleClaim<Guid>>().ToTable("role_claims");
    }
}
