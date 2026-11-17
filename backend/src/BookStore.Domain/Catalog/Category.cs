using BookStore.Domain.Common;

namespace BookStore.Domain.Catalog;

/// <summary>A book category, named in each UI language so it can be shown in ar/fr/en.</summary>
public sealed class Category : Entity
{
    private readonly List<Book> _books = [];

    private Category()
    {
        NameAr = NameFr = NameEn = Slug = string.Empty;
    }

    public Category(string nameAr, string nameFr, string nameEn, string slug)
    {
        NameAr = nameAr;
        NameFr = nameFr;
        NameEn = nameEn;
        Slug = slug;
    }

    public string NameAr { get; private set; }
    public string NameFr { get; private set; }
    public string NameEn { get; private set; }
    public string Slug { get; private set; }

    public IReadOnlyCollection<Book> Books => _books;

    public void Update(string nameAr, string nameFr, string nameEn, string slug)
    {
        NameAr = nameAr;
        NameFr = nameFr;
        NameEn = nameEn;
        Slug = slug;
    }
}
