using BookStore.Domain.Shipping;
using BookStore.Infrastructure.Persistence.Seed;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BookStore.Infrastructure.Persistence.Configurations;

internal sealed class WilayaConfiguration : IEntityTypeConfiguration<Wilaya>
{
    public void Configure(EntityTypeBuilder<Wilaya> builder)
    {
        builder.HasKey(w => w.Code);
        builder.Property(w => w.Code).ValueGeneratedNever();
        builder.Property(w => w.NameAr).HasMaxLength(100).IsRequired();
        builder.Property(w => w.NameFr).HasMaxLength(100).IsRequired();

        builder.ToTable(t => t.HasCheckConstraint("ck_wilayas_shipping_fee", "shipping_fee >= 0"));

        // Reference data lives in the migration itself, so every environment (local, Supabase) has it.
        builder.HasData(WilayaSeedData.All);
    }
}
