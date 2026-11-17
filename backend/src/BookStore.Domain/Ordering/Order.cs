using BookStore.Domain.Common;

namespace BookStore.Domain.Ordering;

public enum OrderStatus
{
    /// <summary>Placed, stock reserved, waiting for the payment webhook.</summary>
    Pending,
    Paid,
    Shipped,
    Delivered,
    /// <summary>Cancelled by the customer at the payment page, by the admin, or because the reservation expired.</summary>
    Cancelled,
    /// <summary>The payment was declined.</summary>
    Failed,
}

/// <summary>Delivery address, copied onto the order so later profile changes don't alter past orders.</summary>
public sealed record ShippingAddress(
    string FullName,
    string Phone,
    int WilayaCode,
    string WilayaName,
    string Commune,
    string AddressLine);

/// <summary>A line of an order. Title and unit price are snapshots taken when the order was placed.</summary>
public sealed class OrderItem : Entity
{
    private OrderItem()
    {
        Title = string.Empty;
    }

    public OrderItem(Guid bookId, string title, int unitPrice, int quantity)
    {
        if (quantity <= 0)
        {
            throw new DomainException("Quantity must be positive.");
        }

        if (unitPrice <= 0)
        {
            throw new DomainException("Unit price must be positive.");
        }

        BookId = bookId;
        Title = title;
        UnitPrice = unitPrice;
        Quantity = quantity;
    }

    public Guid OrderId { get; private set; }
    public Guid BookId { get; private set; }
    public string Title { get; private set; }
    public int UnitPrice { get; private set; }
    public int Quantity { get; private set; }
    public int LineTotal => UnitPrice * Quantity;
}

/// <summary>
/// A customer order and its lifecycle. Stock is reserved when the order is placed
/// (<see cref="StockReserved"/> = true) and released when the order fails, is cancelled or expires.
/// </summary>
public sealed class Order : AuditableEntity
{
    private readonly List<OrderItem> _items = [];

    private Order()
    {
        OrderNumber = string.Empty;
        ShippingAddress = null!;
    }

    private Order(string orderNumber, Guid userId, ShippingAddress address, List<OrderItem> items, int shippingFee, DateTimeOffset now, TimeSpan reservation)
    {
        OrderNumber = orderNumber;
        UserId = userId;
        ShippingAddress = address;
        _items = items;
        ShippingFee = shippingFee;
        Subtotal = items.Sum(i => i.LineTotal);
        Total = Subtotal + shippingFee;
        Status = OrderStatus.Pending;
        PlacedAt = now;
        ExpiresAt = now + reservation;
        StockReserved = true;
    }

    public string OrderNumber { get; private set; }
    public Guid UserId { get; private set; }
    public OrderStatus Status { get; private set; }
    public int Subtotal { get; private set; }
    public int ShippingFee { get; private set; }
    public int Total { get; private set; }
    public ShippingAddress ShippingAddress { get; private set; }

    public string? PaymentProvider { get; private set; }

    /// <summary>The provider's checkout id (Chargily checkout id); webhooks are matched on it.</summary>
    public string? PaymentCheckoutId { get; private set; }
    public string? PaymentUrl { get; private set; }

    public DateTimeOffset PlacedAt { get; private set; }

    /// <summary>When the stock reservation lapses if no payment has been confirmed.</summary>
    public DateTimeOffset ExpiresAt { get; private set; }
    public DateTimeOffset? PaidAt { get; private set; }
    public DateTimeOffset? ShippedAt { get; private set; }
    public DateTimeOffset? DeliveredAt { get; private set; }

    /// <summary>True while this order holds stock (Pending or Paid/Shipped/Delivered).</summary>
    public bool StockReserved { get; private set; }

    /// <summary>Set when a payment arrived for an order whose stock could no longer be secured: the admin must refund or restock.</summary>
    public bool RequiresAttention { get; private set; }

    /// <summary>Internal explanation for the last status change (expired, declined, ...).</summary>
    public string? StatusNote { get; private set; }

    /// <summary>PostgreSQL xmin: two concurrent status changes (webhook vs expiry job) cannot both win.</summary>
    public uint Version { get; private set; }

    public IReadOnlyCollection<OrderItem> Items => _items;

    public static Order Place(
        string orderNumber,
        Guid userId,
        ShippingAddress address,
        IEnumerable<OrderItem> items,
        int shippingFee,
        DateTimeOffset now,
        TimeSpan reservation)
    {
        var lines = items.ToList();
        if (lines.Count == 0)
        {
            throw new DomainException("An order needs at least one item.");
        }

        if (shippingFee < 0)
        {
            throw new DomainException("Shipping fee cannot be negative.");
        }

        return new Order(orderNumber, userId, address, lines, shippingFee, now, reservation);
    }

    public void AttachPayment(string provider, string checkoutId, string paymentUrl)
    {
        PaymentProvider = provider;
        PaymentCheckoutId = checkoutId;
        PaymentUrl = paymentUrl;
    }

    /// <summary>
    /// Records a confirmed payment. <paramref name="stockSecured"/> tells whether the stock is held
    /// (always true for a Pending order; for a late payment on a released order, whether re-reserving succeeded).
    /// Returns false if the order was already paid (duplicate notification).
    /// </summary>
    public bool MarkPaid(DateTimeOffset now, bool stockSecured)
    {
        if (Status is OrderStatus.Paid or OrderStatus.Shipped or OrderStatus.Delivered)
        {
            return false;
        }

        if (Status == OrderStatus.Pending && !StockReserved)
        {
            throw new DomainException("A pending order must hold its stock.");
        }

        var wasReleased = Status != OrderStatus.Pending;
        Status = OrderStatus.Paid;
        PaidAt = now;
        StockReserved = stockSecured;
        RequiresAttention = !stockSecured;
        StatusNote = wasReleased
            ? stockSecured ? "Paid after the reservation had been released; stock re-reserved." : "Paid but stock is no longer available: refund or restock."
            : null;
        return true;
    }

    /// <summary>Payment declined or cancelled. Returns true if the caller must release the reserved stock.</summary>
    public bool FailPayment(bool cancelledByCustomer, string note)
    {
        if (Status != OrderStatus.Pending)
        {
            return false;
        }

        Status = cancelledByCustomer ? OrderStatus.Cancelled : OrderStatus.Failed;
        StatusNote = note;
        return ReleaseStock();
    }

    /// <summary>The reservation lapsed without payment. Returns true if the caller must release the stock.</summary>
    public bool Expire(DateTimeOffset now)
    {
        if (Status != OrderStatus.Pending || now < ExpiresAt)
        {
            return false;
        }

        Status = OrderStatus.Cancelled;
        StatusNote = "Reservation expired without payment.";
        return ReleaseStock();
    }

    // ---------------- Admin actions ----------------

    /// <summary>Paid → Shipped. A paid order without secured stock must be resolved first.</summary>
    public void Ship(DateTimeOffset now)
    {
        if (Status != OrderStatus.Paid)
        {
            throw new DomainException($"Only paid orders can be shipped (current status: {Status}).");
        }

        if (RequiresAttention)
        {
            throw new DomainException("This order needs attention (stock not secured) before it can be shipped.");
        }

        Status = OrderStatus.Shipped;
        ShippedAt = now;
    }

    /// <summary>Shipped → Delivered.</summary>
    public void Deliver(DateTimeOffset now)
    {
        if (Status != OrderStatus.Shipped)
        {
            throw new DomainException($"Only shipped orders can be marked delivered (current status: {Status}).");
        }

        Status = OrderStatus.Delivered;
        DeliveredAt = now;
    }

    /// <summary>
    /// Admin cancellation (Pending, Paid or Shipped). Returns true if the caller must release reserved stock.
    /// Cancelling a paid order means the customer must be refunded from the Chargily dashboard.
    /// </summary>
    public bool CancelByAdmin(string note)
    {
        if (Status is OrderStatus.Delivered or OrderStatus.Cancelled or OrderStatus.Failed)
        {
            throw new DomainException($"An order in status {Status} cannot be cancelled.");
        }

        Status = OrderStatus.Cancelled;
        StatusNote = note;
        RequiresAttention = false;
        return ReleaseStock();
    }

    /// <summary>The admin handled a flagged order (e.g. restocked, so it can now be shipped).</summary>
    public void ResolveAttention(string note)
    {
        if (!RequiresAttention)
        {
            return;
        }

        RequiresAttention = false;
        StockReserved = true;
        StatusNote = note;
    }

    private bool ReleaseStock()
    {
        var mustRelease = StockReserved;
        StockReserved = false;
        return mustRelease;
    }
}
