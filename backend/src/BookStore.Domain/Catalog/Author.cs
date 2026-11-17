using BookStore.Domain.Common;

namespace BookStore.Domain.Catalog;

public sealed class Author : Entity
{
    private readonly List<Book> _books = [];

    private Author()
    {
        Name = Slug = string.Empty;
    }

    public Author(string name, string slug, string? bio = null)
    {
        Name = name;
        Slug = slug;
        Bio = bio;
    }

    /// <summary>Name as printed on the cover (Arabic or Latin script).</summary>
    public string Name { get; private set; }
    public string Slug { get; private set; }
    public string? Bio { get; private set; }

    public IReadOnlyCollection<Book> Books => _books;

    public void Update(string name, string slug, string? bio)
    {
        Name = name;
        Slug = slug;
        Bio = bio;
    }
}
