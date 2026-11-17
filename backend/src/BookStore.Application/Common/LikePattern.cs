namespace BookStore.Application.Common;

public static class LikePattern
{
    /// <summary>
    /// Builds a "%term%" pattern for ILIKE, escaping the LIKE wildcards (% and _) and the escape
    /// character itself so user input is matched literally.
    /// </summary>
    public static string Contains(string term)
    {
        var escaped = term.Trim()
            .Replace(@"\", @"\\", StringComparison.Ordinal)
            .Replace("%", @"\%", StringComparison.Ordinal)
            .Replace("_", @"\_", StringComparison.Ordinal);
        return $"%{escaped}%";
    }
}
