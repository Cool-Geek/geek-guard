namespace GeekGuard.Core.Data.Migrations;

/// <summary>One SQL script that moves a module's schema forward by one version.</summary>
/// <param name="Module">Who owns the tables, e.g. "core" or a plugin id. Versions are counted per module.</param>
/// <param name="Version">Applied in ascending order; never reuse or renumber a version once released.</param>
/// <param name="Name">Human-readable name, taken from the file name.</param>
/// <param name="Sql">The script. Runs inside a transaction.</param>
public sealed record Migration(string Module, int Version, string Name, string Sql);

/// <summary>Supplies the migrations of one module. The core and every plugin register one.</summary>
public interface IMigrationSource
{
    string Module { get; }

    IReadOnlyList<Migration> GetMigrations();
}
