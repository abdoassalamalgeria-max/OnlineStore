using BookStore.Domain.Common;

namespace BookStore.Domain.Catalog;

/// <summary>A sellable book. Money is whole Algerian dinars (DZD).</summary>
public sealed class Book : AuditableEntity
{
    private readonly List<Author> _authors = [];
    private readonly List<Category> _categories = [];

    // For EF Core.
    private Book()
    {
        Title = Slug = Description = Isbn = Language = Publisher = string.Empty;
    }

    public Book(
        string title,
        string slug,
        string description,
        string isbn,
        int price,
        int stock,
        int pageCount,
        string language,
        DateOnly? publishedOn,
        string publisher)
    {
        Title = title;
        Slug = slug;
        Description = description;
        Isbn = isbn;
        PageCount = pageCount;
        Language = language;
        PublishedOn = publishedOn;
        Publisher = publisher;
        SetPrice(price, salePrice: null);
        SetStock(stock);
    }

    public string Title { get; private set; }
    public string Slug { get; private set; }
    public string Description { get; private set; }
    public string Isbn { get; private set; }
    public int Price { get; private set; }

    /// <summary>Discounted price; null when the book is not on sale. Always lower than <see cref="Price"/>.</summary>
    public int? SalePrice { get; private set; }

    /// <summary>
    /// What the customer pays: SalePrice ?? Price. Stored as a generated column in PostgreSQL
    /// so filtering and sorting by price can use an index.
    /// </summary>
    public int EffectivePrice { get; private set; }

    public int Stock { get; private set; }
    public string? CoverImageUrl { get; private set; }
    public int PageCount { get; private set; }
    public string Language { get; private set; }
    public DateOnly? PublishedOn { get; private set; }
    public string Publisher { get; private set; }

    /// <summary>Shown in the "featured" section of the home page.</summary>
    public bool IsFeatured { get; private set; }

    public IReadOnlyCollection<Author> Authors => _authors;
    public IReadOnlyCollection<Category> Categories => _categories;

    public void SetPrice(int price, int? salePrice)
    {
        if (price <= 0)
        {
            throw new DomainException("Price must be positive.");
        }

        if (salePrice is not null && (salePrice <= 0 || salePrice >= price))
        {
            throw new DomainException("Sale price must be positive and lower than the price.");
        }

        Price = price;
        SalePrice = salePrice;
        EffectivePrice = salePrice ?? price;
    }

    public void SetStock(int stock)
    {
        if (stock < 0)
        {
            throw new DomainException("Stock cannot be negative.");
        }

        Stock = stock;
    }

    public void UpdateDetails(
        string title,
        string slug,
        string description,
        string isbn,
        int pageCount,
        string language,
        DateOnly? publishedOn,
        string publisher)
    {
        if (pageCount <= 0)
        {
            throw new DomainException("Page count must be positive.");
        }

        Title = title;
        Slug = slug;
        Description = description;
        Isbn = isbn;
        PageCount = pageCount;
        Language = language;
        PublishedOn = publishedOn;
        Publisher = publisher;
    }

    /// <summary>Replaces the author list (order preserved as given).</summary>
    public void SetAuthors(IEnumerable<Author> authors)
    {
        _authors.Clear();
        foreach (var author in authors)
        {
            AddAuthor(author);
        }
    }

    public void SetCategories(IEnumerable<Category> categories)
    {
        _categories.Clear();
        foreach (var category in categories)
        {
            AddCategory(category);
        }
    }

    public void SetFeatured(bool isFeatured) => IsFeatured = isFeatured;

    public void SetCoverImage(string? url) => CoverImageUrl = url;

    public void AddAuthor(Author author)
    {
        if (!_authors.Contains(author))
        {
            _authors.Add(author);
        }
    }

    public void AddCategory(Category category)
    {
        if (!_categories.Contains(category))
        {
            _categories.Add(category);
        }
    }
}
