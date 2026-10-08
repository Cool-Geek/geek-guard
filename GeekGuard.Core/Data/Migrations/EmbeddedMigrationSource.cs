using System.Reflection;
using System.Text.RegularExpressions;

namespace GeekGuard.Core.Data.Migrations;

/// <summary>
/// Reads migrations embedded in an assembly. Files must be named "NNNN_description.sql",
/// e.g. "0001_initial.sql"; the number is the version.
/// </summary>
/// <param name="module">Owner of the migrations: "core" or a plugin id.</param>
/// <param name="assembly">Assembly the .sql files are embedded in.</param>
/// <param name="resourceNamespace">
/// Only resources under this namespace are read, e.g. "GeekGuard.Core.Migrations". This lets several
/// plugins that live in one assembly keep separate migration folders.
/// </param>
public sealed partial class EmbeddedMigrationSource(string module, Assembly assembly, string resourceNamespace) : IMigrationSource
{
    [GeneratedRegex(@"^(?<version>\d{4})_(?<name>[a-z0-9_]+)\.sql$", RegexOptions.IgnoreCase)]
    private static partial Regex FileName();

    public string Module { get; } = module;

    public IReadOnlyList<Migration> GetMigrations()
    {
        var prefix = resourceNamespace + ".";
        var migrations = new List<Migration>();
        foreach (var resource in assembly.GetManifestResourceNames())
        {
            if (!resource.StartsWith(prefix, StringComparison.Ordinal)) continue;

            // Only direct children: "X.Migrations.0001_initial.sql", not "X.Migrations.Sub.0001_a.sql".
            var match = FileName().Match(resource[prefix.Length..]);
            if (!match.Success) continue;

            using var stream = assembly.GetManifestResourceStream(resource)!;
            using var reader = new StreamReader(stream);
            migrations.Add(new Migration(
                Module,
                int.Parse(match.Groups["version"].Value),
                match.Groups["name"].Value,
                reader.ReadToEnd()));
        }

        var duplicate = migrations.GroupBy(m => m.Version).FirstOrDefault(g => g.Count() > 1);
        if (duplicate is not null)
            throw new InvalidOperationException($"Module '{Module}' has two migrations with version {duplicate.Key}.");

        return migrations.OrderBy(m => m.Version).ToList();
    }
}
