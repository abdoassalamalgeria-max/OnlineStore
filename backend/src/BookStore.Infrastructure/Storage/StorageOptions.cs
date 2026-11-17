namespace BookStore.Infrastructure.Storage;

/// <summary>Bound from "Storage". Provider "Local" (default, development) or "Supabase" (production).</summary>
public sealed class StorageOptions
{
    public const string Section = "Storage";

    public string Provider { get; set; } = "Local";

    /// <summary>Local: folder for uploaded files (relative to the API's content root).</summary>
    public string LocalRoot { get; set; } = "wwwroot/uploads";

    /// <summary>Local: absolute URL under which the API serves <see cref="LocalRoot"/> (path must end with /uploads).</summary>
    public string LocalPublicBaseUrl { get; set; } = "http://localhost:5080/uploads";

    /// <summary>Supabase: project URL, e.g. https://abcd1234.supabase.co.</summary>
    public string? SupabaseUrl { get; set; }

    /// <summary>Supabase: service_role key (server-side only; from user-secrets / env Storage__SupabaseServiceKey).</summary>
    public string? SupabaseServiceKey { get; set; }

    /// <summary>Supabase: public bucket holding the covers.</summary>
    public string SupabaseBucket { get; set; } = "book-covers";

    public bool UseSupabase => string.Equals(Provider, "Supabase", StringComparison.OrdinalIgnoreCase);
}
