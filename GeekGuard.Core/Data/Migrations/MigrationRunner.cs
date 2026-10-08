using System.Text.RegularExpressions;
using Dapper;
using Microsoft.Extensions.Options;
using Npgsql;

namespace GeekGuard.Core.Data.Migrations;

/// <summary>
/// Brings the database schema up to date at startup: creates the database if it is missing,
/// then applies every migration not yet recorded in the schema_migrations table, module by module.
/// </summary>
public sealed partial class MigrationRunner(
    NpgsqlDataSource dataSource,
    IEnumerable<IMigrationSource> sources,
    IOptions<DatabaseOptions> options,
    ILogger<MigrationRunner> log)
{
    /// <summary>Arbitrary constant for pg_advisory_lock, so two bot instances never migrate at once.</summary>
    private const long LockKey = 0x6765656B_67756172; // "geekguar"

    private const int MaxConnectAttempts = 10;

    [GeneratedRegex("^[A-Za-z0-9_]+$")]
    private static partial Regex SafeIdentifier();

    public async Task RunAsync(CancellationToken ct = default)
    {
        await EnsureDatabaseExistsAsync(ct);

        await using var connection = await dataSource.OpenConnectionAsync(ct);
        await connection.ExecuteAsync(
            """
            CREATE TABLE IF NOT EXISTS schema_migrations (
                module     TEXT        NOT NULL,
                version    INT         NOT NULL,
                name       TEXT        NOT NULL,
                applied_at TIMESTAMPTZ NOT NULL DEFAULT now(),
                PRIMARY KEY (module, version)
            )
            """);

        await connection.ExecuteAsync("SELECT pg_advisory_lock(@LockKey)", new { LockKey });
        try
        {
            var applied = (await connection.QueryAsync<(string Module, int Version)>(
                    "SELECT module, version FROM schema_migrations"))
                .ToHashSet();

            var count = 0;
            foreach (var source in sources)
            {
                foreach (var migration in source.GetMigrations())
                {
                    if (applied.Contains((migration.Module, migration.Version))) continue;
                    await ApplyAsync(connection, migration, ct);
                    count++;
                }
            }

            log.LogInformation(count == 0
                ? "Database schema is up to date"
                : "Applied {Count} database migration(s)", count);
        }
        finally
        {
            await connection.ExecuteAsync("SELECT pg_advisory_unlock(@LockKey)", new { LockKey });
        }
    }

    private async Task ApplyAsync(NpgsqlConnection connection, Migration migration, CancellationToken ct)
    {
        log.LogInformation("Applying migration {Module}/{Version:D4}_{Name}", migration.Module, migration.Version, migration.Name);

        // Script and bookkeeping commit together: a failed script leaves no half-applied version behind.
        await using var transaction = await connection.BeginTransactionAsync(ct);
        await connection.ExecuteAsync(new CommandDefinition(migration.Sql, transaction: transaction, cancellationToken: ct));
        await connection.ExecuteAsync(new CommandDefinition(
            "INSERT INTO schema_migrations (module, version, name) VALUES (@Module, @Version, @Name)",
            migration, transaction, cancellationToken: ct));
        await transaction.CommitAsync(ct);
    }

    /// <summary>
    /// Creates the target database on first run. Also waits for PostgreSQL itself,
    /// which may still be starting when the server boots.
    /// </summary>
    private async Task EnsureDatabaseExistsAsync(CancellationToken ct)
    {
        var target = new NpgsqlConnectionStringBuilder(options.Value.ConnectionString);
        var databaseName = target.Database;
        if (string.IsNullOrEmpty(databaseName) || !SafeIdentifier().IsMatch(databaseName))
            throw new InvalidOperationException(
                $"Database name '{databaseName}' must contain only letters, digits and underscores.");

        // Connect to the built-in "postgres" database to check for and create ours.
        var admin = new NpgsqlConnectionStringBuilder(target.ConnectionString) { Database = "postgres", Pooling = false };

        for (var attempt = 1; ; attempt++)
        {
            try
            {
                await using var connection = new NpgsqlConnection(admin.ConnectionString);
                await connection.OpenAsync(ct);

                var exists = await connection.ExecuteScalarAsync<bool>(
                    "SELECT EXISTS (SELECT 1 FROM pg_database WHERE datname = @databaseName)", new { databaseName });
                if (!exists)
                {
                    // Identifiers cannot be parameters; the name was validated above.
                    await connection.ExecuteAsync($"CREATE DATABASE \"{databaseName}\" ENCODING 'UTF8'");
                    log.LogInformation("Created database {Database}", databaseName);
                }
                return;
            }
            catch (NpgsqlException ex) when (attempt < MaxConnectAttempts && ex is not PostgresException)
            {
                // Network-level failure (server not up yet). Authentication or SQL errors are not retried.
                log.LogWarning("PostgreSQL not reachable (attempt {Attempt}): {Message}", attempt, ex.Message);
                await Task.Delay(TimeSpan.FromSeconds(3), ct);
            }
        }
    }
}
