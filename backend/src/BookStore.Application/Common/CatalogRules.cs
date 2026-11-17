using System.Text.RegularExpressions;

namespace BookStore.Application.Common;

public static partial class CatalogRules
{
    /// <summary>URL slugs: lowercase Latin letters, digits and single hyphens ("ce-que-le-jour").</summary>
    public static bool IsValidSlug(string? slug) => slug is not null && SlugRegex().IsMatch(slug);

    /// <summary>Removes hyphens and spaces: "978-0-00-000001-1" → "9780000000011".</summary>
    public static string NormalizeIsbn(string isbn) => isbn.Replace("-", string.Empty, StringComparison.Ordinal).Replace(" ", string.Empty, StringComparison.Ordinal).ToUpperInvariant();

    /// <summary>Validates an ISBN-13 or ISBN-10 including its check digit (input may contain hyphens).</summary>
    public static bool IsValidIsbn(string? isbn)
    {
        if (string.IsNullOrWhiteSpace(isbn))
        {
            return false;
        }

        var digits = NormalizeIsbn(isbn);
        return digits.Length switch
        {
            13 => digits.All(char.IsAsciiDigit) && Isbn13CheckOk(digits),
            10 => digits[..9].All(char.IsAsciiDigit) && (char.IsAsciiDigit(digits[9]) || digits[9] == 'X') && Isbn10CheckOk(digits),
            _ => false,
        };
    }

    private static bool Isbn13CheckOk(string d)
    {
        var sum = 0;
        for (var i = 0; i < 12; i++)
        {
            sum += (d[i] - '0') * (i % 2 == 0 ? 1 : 3);
        }

        return (10 - (sum % 10)) % 10 == d[12] - '0';
    }

    private static bool Isbn10CheckOk(string d)
    {
        var sum = 0;
        for (var i = 0; i < 10; i++)
        {
            var value = d[i] == 'X' ? 10 : d[i] - '0';
            sum += value * (10 - i);
        }

        return sum % 11 == 0;
    }

    [GeneratedRegex("^[a-z0-9]+(?:-[a-z0-9]+)*$")]
    private static partial Regex SlugRegex();
}
