using BookStore.Application.Features.Ordering;
using BookStore.Domain.Common;
using BookStore.Domain.Ordering;
using BookStore.Domain.Shopping;
using FluentValidation.TestHelper;

namespace BookStore.Application.UnitTests;

public sealed class OrderLifecycleRulesTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 16, 10, 0, 0, TimeSpan.Zero);

    private static Order NewOrder() => Order.Place(
        "BK-20261116-TEST01",
        Guid.NewGuid(),
        new ShippingAddress("Amine", "0550123456", 16, "الجزائر", "Bab Ezzouar", "Cité 1"),
        [new OrderItem(Guid.NewGuid(), "L'Étranger", 1100, 2), new OrderItem(Guid.NewGuid(), "Nedjma", 1600, 1)],
        shippingFee: 600,
        Now,
        TimeSpan.FromMinutes(30));

    [Fact]
    public void Placing_computes_totals_and_reserves_stock()
    {
        var order = NewOrder();

        Assert.Equal(OrderStatus.Pending, order.Status);
        Assert.Equal(3800, order.Subtotal);
        Assert.Equal(4400, order.Total);
        Assert.True(order.StockReserved);
        Assert.Equal(Now.AddMinutes(30), order.ExpiresAt);
    }

    [Fact]
    public void Cannot_place_an_empty_order() =>
        Assert.Throws<DomainException>(() => Order.Place("X", Guid.NewGuid(),
            new ShippingAddress("a", "0550123456", 1, "w", "c", "l"), [], 600, Now, TimeSpan.FromMinutes(30)));

    [Fact]
    public void Paying_a_pending_order_keeps_its_stock()
    {
        var order = NewOrder();

        Assert.True(order.MarkPaid(Now.AddMinutes(5), stockSecured: true));
        Assert.Equal(OrderStatus.Paid, order.Status);
        Assert.True(order.StockReserved);
        Assert.False(order.RequiresAttention);
    }

    [Fact]
    public void A_duplicate_payment_is_a_no_op()
    {
        var order = NewOrder();
        order.MarkPaid(Now, stockSecured: true);

        Assert.False(order.MarkPaid(Now.AddMinutes(1), stockSecured: true));
    }

    [Fact]
    public void Failing_a_pending_order_releases_stock_once()
    {
        var order = NewOrder();

        Assert.True(order.FailPayment(cancelledByCustomer: false, "declined"));
        Assert.Equal(OrderStatus.Failed, order.Status);
        Assert.False(order.StockReserved);
        Assert.False(order.FailPayment(cancelledByCustomer: false, "declined again"));
    }

    [Fact]
    public void Customer_cancellation_is_cancelled_not_failed()
    {
        var order = NewOrder();
        order.FailPayment(cancelledByCustomer: true, "canceled");

        Assert.Equal(OrderStatus.Cancelled, order.Status);
    }

    [Fact]
    public void A_failure_after_payment_does_not_undo_the_payment()
    {
        var order = NewOrder();
        order.MarkPaid(Now, stockSecured: true);

        Assert.False(order.FailPayment(cancelledByCustomer: false, "late failure"));
        Assert.Equal(OrderStatus.Paid, order.Status);
        Assert.True(order.StockReserved);
    }

    [Fact]
    public void Expiry_only_happens_after_the_deadline()
    {
        var order = NewOrder();

        Assert.False(order.Expire(Now.AddMinutes(29)));
        Assert.Equal(OrderStatus.Pending, order.Status);

        Assert.True(order.Expire(Now.AddMinutes(30)));
        Assert.Equal(OrderStatus.Cancelled, order.Status);
        Assert.False(order.StockReserved);
    }

    [Fact]
    public void A_paid_order_never_expires()
    {
        var order = NewOrder();
        order.MarkPaid(Now, stockSecured: true);

        Assert.False(order.Expire(Now.AddHours(5)));
        Assert.Equal(OrderStatus.Paid, order.Status);
    }

    [Fact]
    public void Late_payment_with_restocked_items_is_paid_normally()
    {
        var order = NewOrder();
        order.Expire(Now.AddHours(1));

        Assert.True(order.MarkPaid(Now.AddHours(2), stockSecured: true));
        Assert.Equal(OrderStatus.Paid, order.Status);
        Assert.True(order.StockReserved);
        Assert.False(order.RequiresAttention);
    }

    [Fact]
    public void Late_payment_without_stock_is_paid_but_flagged_for_the_admin()
    {
        var order = NewOrder();
        order.Expire(Now.AddHours(1));

        order.MarkPaid(Now.AddHours(2), stockSecured: false);

        Assert.Equal(OrderStatus.Paid, order.Status);
        Assert.False(order.StockReserved);
        Assert.True(order.RequiresAttention);
    }
}

public sealed class CartRulesTests
{
    [Fact]
    public void Adding_the_same_book_sums_quantities()
    {
        var cart = new Cart(Guid.NewGuid());
        var book = Guid.NewGuid();

        cart.Add(book, 2);
        cart.Add(book, 3);

        Assert.Equal(5, Assert.Single(cart.Items).Quantity);
    }

    [Fact]
    public void Quantity_is_capped_per_line()
    {
        var cart = new Cart(Guid.NewGuid());
        var book = Guid.NewGuid();

        cart.Add(book, 15);
        cart.Add(book, 15);

        Assert.Equal(Cart.MaxQuantityPerItem, Assert.Single(cart.Items).Quantity);
    }

    [Fact]
    public void Setting_quantity_to_zero_removes_the_line()
    {
        var cart = new Cart(Guid.NewGuid());
        var book = Guid.NewGuid();
        cart.Add(book, 1);

        Assert.True(cart.SetQuantity(book, 0));
        Assert.Empty(cart.Items);
    }

    [Fact]
    public void Updating_a_missing_line_reports_false() =>
        Assert.False(new Cart(Guid.NewGuid()).SetQuantity(Guid.NewGuid(), 2));

    [Fact]
    public void Non_positive_quantities_are_rejected() =>
        Assert.Throws<DomainException>(() => new Cart(Guid.NewGuid()).Add(Guid.NewGuid(), 0));
}

public sealed class CheckoutRequestValidatorTests
{
    private readonly CheckoutRequestValidator _validator = new();

    private static CheckoutRequest Valid(int wilaya = 16, string phone = "0550123456", string locale = "ar") =>
        new(new ShippingAddressRequest("Amine Benali", phone, wilaya, "Bab Ezzouar", "Cité 1, Bt 3"), locale);

    [Fact]
    public void Valid_request_passes() => _validator.TestValidate(Valid()).ShouldNotHaveAnyValidationErrors();

    [Theory]
    [InlineData(0)]
    [InlineData(59)]
    public void Wilaya_must_be_one_of_the_58(int code) =>
        _validator.TestValidate(Valid(wilaya: code)).ShouldHaveValidationErrorFor("ShippingAddress.WilayaCode");

    [Fact]
    public void Phone_must_be_algerian() =>
        _validator.TestValidate(Valid(phone: "+33612345678")).ShouldHaveValidationErrorFor("ShippingAddress.Phone");

    [Fact]
    public void Locale_must_be_supported() =>
        _validator.TestValidate(Valid(locale: "de")).ShouldHaveValidationErrorFor(r => r.Locale);

    [Fact]
    public void Address_fields_are_required()
    {
        var result = _validator.TestValidate(new CheckoutRequest(new ShippingAddressRequest("", "0550123456", 16, "", ""), "ar"));

        result.ShouldHaveValidationErrorFor("ShippingAddress.FullName");
        result.ShouldHaveValidationErrorFor("ShippingAddress.Commune");
        result.ShouldHaveValidationErrorFor("ShippingAddress.AddressLine");
    }
}
