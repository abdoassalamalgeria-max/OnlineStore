namespace BookStore.Domain.Catalog;

/// <summary>Languages a book can be written in, as lowercase codes stored in the database.</summary>
public static class BookLanguages
{
    public const string Arabic = "ar";
    public const string French = "fr";
    public const string English = "en";
    public const string Tamazight = "ber";

    public static readonly IReadOnlySet<string> All = new HashSet<string>(StringComparer.Ordinal)
    {
        Arabic, French, English, Tamazight,
    };

    public static bool IsValid(string? code) => code is not null && All.Contains(code);
}
