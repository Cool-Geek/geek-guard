namespace GeekGuard.Core.Data;

/// <summary>Database settings. The connection string is read from "ConnectionStrings:GeekGuard".</summary>
public sealed class DatabaseOptions
{
    public const string ConnectionStringName = "GeekGuard";

    /// <summary>
    /// Npgsql connection string, e.g. "Host=localhost;Port=5432;Database=geekguard;Username=postgres;Password=...".
    /// Secret: keep it in User Secrets or the environment variable ConnectionStrings__GeekGuard.
    /// </summary>
    public string ConnectionString { get; set; } = "";
}
