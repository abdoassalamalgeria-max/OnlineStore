using BookStore.Domain.Catalog;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BookStore.Infrastructure.Persistence.Configurations;

internal sealed class AuthorConfiguration : IEntityTypeConfiguration<Author>
{
    public void Configure(EntityTypeBuilder<Author> builder)
    {
        builder.Property(a => a.Name).HasMaxLength(200).IsRequired();
        builder.Property(a => a.Slug).HasMaxLength(200).IsRequired();
        builder.Property(a => a.Bio).HasMaxLength(2000);

        builder.HasIndex(a => a.Slug).IsUnique();
        builder.HasIndex(a => a.Name)
            .HasMethod("gin")
            .HasOperators("gin_trgm_ops")
            .HasDatabaseName("ix_authors_name_trgm");

        builder.Navigation(a => a.Books).UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}
