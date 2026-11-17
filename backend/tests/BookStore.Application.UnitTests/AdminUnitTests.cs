using BookStore.Application.Common;
using BookStore.Application.Features.Admin;
using BookStore.Domain.Common;
using BookStore.Domain.Ordering;
using FluentValidation.TestHelper;

namespace BookStore.Application.UnitTests;

public sealed class CatalogRulesTests
{
    [Theory]
    [InlineData("978-0-306-40615-7", true)]   // classic ISBN-13 example
    [InlineData("9780306406157", true)]
    [InlineData("978-0-306-40615-8", false)]  // wrong check digit
    [InlineData("0-306-40615-2", true)]       // ISBN-10
    [InlineData("0-8044-2957-X", true)]       // ISBN-10 with X check digit
    [InlineData("0-8044-2957-5", false)]
    [InlineData("12345", false)]
    [InlineData("", false)]
    public void Validates_isbn_check_digits(string isbn, bool valid) => Assert.Equal(valid, CatalogRules.IsValidIsbn(isbn));

    [Fact]
    public void Normalizes_isbn() => Assert.Equal("9780306406157", CatalogRules.NormalizeIsbn("978-0 306-40615-7"));

    [Theory]
    [InlineData("ce-que-le-jour", true)]
    [InlineData("1984", true)]
    [InlineData("Nedjma", false)]       // uppercase
    [InlineData("la--peste", false)]    // double hyphen
    [InlineData("-peste", false)]
    [InlineData("ذاكرة", false)]        // slugs are Latin so URLs stay readable everywhere
    [InlineData("l'etranger", false)]
    public void Validates_slugs(string slug, bool valid) => Assert.Equal(valid, CatalogRules.IsValidSlug(slug));
}

public sealed class BookUpsertValidatorTests
{
    private static BookUpsertRequest Valid() => new(
        "Nedjma", "nedjma", "Roman.", "978-0-306-40615-7", 1600, null, 10, 256, "fr",
        new DateOnly(1956, 1, 1), "Éditions", false, [Guid.NewGuid()], [Guid.NewGuid()]);

    private readonly BookUpsertRequestValidator _validator = new();

    [Fact]
    public void Valid_request_passes() => _validator.TestValidate(Valid()).ShouldNotHaveAnyValidationErrors();

    [Fact]
    public void Sale_price_must_be_below_price() =>
        _validator.TestValidate(Valid() with { SalePrice = 1600 }).ShouldHaveValidationErrorFor(b => b.SalePrice);

    [Fact]
    public void Needs_authors_and_categories()
    {
        var result = _validator.TestValidate(Valid() with { AuthorIds = [], CategoryIds = [] });
        result.ShouldHaveValidationErrorFor(b => b.AuthorIds);
        result.ShouldHaveValidationErrorFor(b => b.CategoryIds);
    }

    [Fact]
    public void Rejects_bad_isbn_slug_and_language()
    {
        var result = _validator.TestValidate(Valid() with { Isbn = "123", Slug = "Bad Slug", Language = "de" });
        result.ShouldHaveValidationErrorFor(b => b.Isbn);
        result.ShouldHaveValidationErrorFor(b => b.Slug);
        result.ShouldHaveValidationErrorFor(b => b.Language);
    }
}

public sealed class AdminOrderRulesTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 16, 10, 0, 0, TimeSpan.Zero);

    private static Order NewOrder() => Order.Place(
        "BK-TEST", Guid.NewGuid(), new ShippingAddress("A", "0550123456", 16, "الجزائر", "c", "l"),
        [new OrderItem(Guid.NewGuid(), "Book", 1000, 1)], 600, Now, TimeSpan.FromMinutes(30));

    [Fact]
    public void Paid_order_ships_then_delivers()
    {
        var order = NewOrder();
        order.MarkPaid(Now, stockSecured: true);

        order.Ship(Now.AddDays(1));
        Assert.Equal(OrderStatus.Shipped, order.Status);
        order.Deliver(Now.AddDays(3));
        Assert.Equal(OrderStatus.Delivered, order.Status);
        Assert.NotNull(order.ShippedAt);
        Assert.NotNull(order.DeliveredAt);
    }

    [Fact]
    public void Unpaid_order_cannot_ship() => Assert.Throws<DomainException>(() => NewOrder().Ship(Now));

    [Fact]
    public void Cannot_deliver_before_shipping()
    {
        var order = NewOrder();
        order.MarkPaid(Now, stockSecured: true);
        Assert.Throws<DomainException>(() => order.Deliver(Now));
    }

    [Fact]
    public void Flagged_order_cannot_ship_until_resolved()
    {
        var order = NewOrder();
        order.Expire(Now.AddHours(1));
        order.MarkPaid(Now.AddHours(2), stockSecured: false);

        Assert.Throws<DomainException>(() => order.Ship(Now));
        order.ResolveAttention("restocked");
        order.Ship(Now);
        Assert.Equal(OrderStatus.Shipped, order.Status);
    }

    [Fact]
    public void Cancelling_a_paid_order_releases_its_stock()
    {
        var order = NewOrder();
        order.MarkPaid(Now, stockSecured: true);

        Assert.True(order.CancelByAdmin("customer asked"));
        Assert.Equal(OrderStatus.Cancelled, order.Status);
        Assert.False(order.StockReserved);
    }

    [Fact]
    public void Delivered_orders_cannot_be_cancelled()
    {
        var order = NewOrder();
        order.MarkPaid(Now, stockSecured: true);
        order.Ship(Now);
        order.Deliver(Now);

        Assert.Throws<DomainException>(() => order.CancelByAdmin("too late"));
    }
}
