using BookStore.Domain.Catalog;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BookStore.Infrastructure.Persistence.Configurations;

internal sealed class CategoryConfiguration : IEntityTypeConfiguration<Category>
{
    public void Configure(EntityTypeBuilder<Category> builder)
    {
        builder.Property(c => c.NameAr).HasMaxLength(100).IsRequired();
        builder.Property(c => c.NameFr).HasMaxLength(100).IsRequired();
        builder.Property(c => c.NameEn).HasMaxLength(100).IsRequired();
        builder.Property(c => c.Slug).HasMaxLength(100).IsRequired();

        builder.HasIndex(c => c.Slug).IsUnique();

        builder.Navigation(c => c.Books).UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}
