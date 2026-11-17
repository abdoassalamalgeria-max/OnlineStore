using BookStore.Domain.Catalog;
using BookStore.Domain.Ordering;
using BookStore.Domain.Payments;
using BookStore.Domain.Shopping;
using BookStore.Infrastructure.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BookStore.Infrastructure.Persistence.Configurations;

internal sealed class CartConfiguration : IEntityTypeConfiguration<Cart>
{
    public void Configure(EntityTypeBuilder<Cart> builder)
    {
        builder.HasIndex(c => c.UserId).IsUnique();
        builder.HasOne<ApplicationUser>().WithMany().HasForeignKey(c => c.UserId).OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(c => c.Items).WithOne().HasForeignKey(i => i.CartId).OnDelete(DeleteBehavior.Cascade);
        builder.Navigation(c => c.Items).UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}

internal sealed class CartItemConfiguration : IEntityTypeConfiguration<CartItem>
{
    public void Configure(EntityTypeBuilder<CartItem> builder)
    {
        builder.HasIndex(i => new { i.CartId, i.BookId }).IsUnique();
        // A deleted book silently disappears from carts.
        builder.HasOne<Book>().WithMany().HasForeignKey(i => i.BookId).OnDelete(DeleteBehavior.Cascade);
        builder.ToTable("cart_items", t => t.HasCheckConstraint("ck_cart_items_quantity", $"quantity BETWEEN 1 AND {Cart.MaxQuantityPerItem}"));
    }
}

internal sealed class OrderConfiguration : IEntityTypeConfiguration<Order>
{
    public void Configure(EntityTypeBuilder<Order> builder)
    {
        builder.Property(o => o.OrderNumber).HasMaxLength(32).IsRequired();
        builder.Property(o => o.Status).HasConversion<string>().HasMaxLength(16);
        builder.Property(o => o.PaymentProvider).HasMaxLength(32);
        builder.Property(o => o.PaymentCheckoutId).HasMaxLength(128);
        builder.Property(o => o.PaymentUrl).HasMaxLength(1000);
        builder.Property(o => o.StatusNote).HasMaxLength(500);
        builder.Property(o => o.Version).IsRowVersion(); // xmin

        builder.OwnsOne(o => o.ShippingAddress, address =>
        {
            address.Property(a => a.FullName).HasColumnName("ship_full_name").HasMaxLength(100).IsRequired();
            address.Property(a => a.Phone).HasColumnName("ship_phone").HasMaxLength(20).IsRequired();
            address.Property(a => a.WilayaCode).HasColumnName("ship_wilaya_code");
            address.Property(a => a.WilayaName).HasColumnName("ship_wilaya_name").HasMaxLength(100).IsRequired();
            address.Property(a => a.Commune).HasColumnName("ship_commune").HasMaxLength(100).IsRequired();
            address.Property(a => a.AddressLine).HasColumnName("ship_address_line").HasMaxLength(300).IsRequired();
        });
        builder.Navigation(o => o.ShippingAddress).IsRequired();

        builder.HasMany(o => o.Items).WithOne().HasForeignKey(i => i.OrderId).OnDelete(DeleteBehavior.Cascade);
        builder.Navigation(o => o.Items).UsePropertyAccessMode(PropertyAccessMode.Field);

        // Orders are financial records: deleting a user must not silently delete them.
        builder.HasOne<ApplicationUser>().WithMany().HasForeignKey(o => o.UserId).OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(o => o.OrderNumber).IsUnique();
        builder.HasIndex(o => new { o.UserId, o.PlacedAt });
        builder.HasIndex(o => new { o.Status, o.ExpiresAt }); // expiry sweep
        builder.HasIndex(o => new { o.PaymentProvider, o.PaymentCheckoutId })
            .IsUnique()
            .HasFilter("payment_checkout_id IS NOT NULL"); // webhook lookup

        builder.ToTable(t =>
        {
            t.HasCheckConstraint("ck_orders_total", "total = subtotal + shipping_fee AND subtotal > 0 AND shipping_fee >= 0");
        });
    }
}

internal sealed class OrderItemConfiguration : IEntityTypeConfiguration<OrderItem>
{
    public void Configure(EntityTypeBuilder<OrderItem> builder)
    {
        builder.Ignore(i => i.LineTotal);
        builder.Property(i => i.Title).HasMaxLength(300).IsRequired();
        // A book that was ever ordered cannot be hard-deleted (keeps order history intact).
        builder.HasOne<Book>().WithMany().HasForeignKey(i => i.BookId).OnDelete(DeleteBehavior.Restrict);
        builder.ToTable("order_items", t => t.HasCheckConstraint("ck_order_items_positive", "quantity > 0 AND unit_price > 0"));
    }
}

internal sealed class PaymentEventConfiguration : IEntityTypeConfiguration<PaymentEvent>
{
    public void Configure(EntityTypeBuilder<PaymentEvent> builder)
    {
        builder.Property(e => e.Provider).HasMaxLength(32).IsRequired();
        builder.Property(e => e.ProviderEventId).HasMaxLength(128).IsRequired();
        builder.Property(e => e.Type).HasMaxLength(32).IsRequired();
        builder.HasIndex(e => new { e.Provider, e.ProviderEventId }).IsUnique(); // idempotency
        builder.HasIndex(e => e.OrderId);
    }
}
