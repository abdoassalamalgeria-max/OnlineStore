using Npgsql;

namespace BookStore.Infrastructure.Persistence;

/// <summary>
/// Normalises the configured connection string for the environments we run in:
/// local Docker PostgreSQL and Supabase through its Session Pooler (Supavisor).
/// </summary>
internal static class DatabaseConnection
{
    /// <summary>Supavisor's transaction-mode port. Session mode (what we use) listens on 5432.</summary>
    public const int SupabaseTransactionPoolerPort = 6543;

    public static string Build(string connectionString, int? maxPoolSize)
    {
        var builder = new NpgsqlConnectionStringBuilder(connectionString);
        var host = builder.Host ?? string.Empty;

        if (IsSupabasePooler(host) && builder.Port == SupabaseTransactionPoolerPort)
        {
            // Transaction mode hands a different server connection to every transaction: no session
            // state, no advisory locks (EF migrations use one) and no prepared statements across calls.
            throw new InvalidOperationException(
                "ConnectionStrings:Default points at Supabase's transaction pooler (port 6543). " +
                "Use the Session pooler string (same host, port 5432) — see README.");
        }

        if (IsSupabase(host) && builder.SslMode is SslMode.Disable or SslMode.Allow or SslMode.Prefer)
        {
            // Supabase always speaks TLS; refuse to silently fall back to plaintext.
            builder.SslMode = SslMode.Require;
        }

        // Npgsql's default (Prefer) probes Kerberos/GSS encryption, which needs libgssapi_krb5 (absent
        // from the aspnet image: "Cannot load library ..." on every start). Neither Supabase nor our
        // local PostgreSQL uses Kerberos.
        if (builder.GssEncryptionMode == GssEncryptionMode.Prefer)
        {
            builder.GssEncryptionMode = GssEncryptionMode.Disable;
        }

        // Each pooled Npgsql connection holds one Supavisor client slot in session mode, and the free
        // plan only has a handful: keep the pool small (Database:MaxPoolSize, 10 by default).
        if (maxPoolSize is > 0)
        {
            builder.MaxPoolSize = maxPoolSize.Value;
        }

        return builder.ConnectionString;
    }

    private static bool IsSupabasePooler(string host) =>
        host.EndsWith(".pooler.supabase.com", StringComparison.OrdinalIgnoreCase);

    private static bool IsSupabase(string host) =>
        IsSupabasePooler(host) || host.EndsWith(".supabase.co", StringComparison.OrdinalIgnoreCase);
}
