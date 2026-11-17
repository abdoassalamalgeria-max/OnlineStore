using BookStore.Domain.Common;

namespace BookStore.Domain.Shopping;

/// <summary>A signed-in customer's cart (guests keep theirs in browser storage until they sign in).</summary>
public sealed class Cart : Entity
{
    /// <summary>Upper bound per line, so one customer cannot reserve a whole print run.</summary>
    public const int MaxQuantityPerItem = 20;

    private readonly List<CartItem> _items = [];

    private Cart()
    {
    }

    public Cart(Guid userId) => UserId = userId;

    public Guid UserId { get; private set; }
    public DateTimeOffset UpdatedAt { get; set; }
    public IReadOnlyCollection<CartItem> Items => _items;

    /// <summary>Adds to the line (or creates it), capping at <see cref="MaxQuantityPerItem"/>.</summary>
    public void Add(Guid bookId, int quantity)
    {
        if (quantity <= 0)
        {
            throw new DomainException("Quantity must be positive.");
        }

        var line = _items.FirstOrDefault(i => i.BookId == bookId);
        if (line is null)
        {
            _items.Add(new CartItem(bookId, Math.Min(quantity, MaxQuantityPerItem)));
        }
        else
        {
            line.SetQuantity(Math.Min(line.Quantity + quantity, MaxQuantityPerItem));
        }
    }

    /// <summary>Sets an existing line's quantity; 0 removes the line. Returns false if the book is not in the cart.</summary>
    public bool SetQuantity(Guid bookId, int quantity)
    {
        var line = _items.FirstOrDefault(i => i.BookId == bookId);
        if (line is null)
        {
            return false;
        }

        if (quantity <= 0)
        {
            _items.Remove(line);
        }
        else
        {
            line.SetQuantity(Math.Min(quantity, MaxQuantityPerItem));
        }

        return true;
    }

    public bool Remove(Guid bookId) => _items.RemoveAll(i => i.BookId == bookId) > 0;

    public void Clear() => _items.Clear();
}

public sealed class CartItem : Entity
{
    private CartItem()
    {
    }

    internal CartItem(Guid bookId, int quantity)
    {
        BookId = bookId;
        Quantity = quantity;
    }

    public Guid CartId { get; private set; }
    public Guid BookId { get; private set; }
    public int Quantity { get; private set; }

    internal void SetQuantity(int quantity) => Quantity = quantity;
}
