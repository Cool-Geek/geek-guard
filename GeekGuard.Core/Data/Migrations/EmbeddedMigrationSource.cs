using System.Reflection;
using System.Text.RegularExpressions;

namespace GeekGuard.Core.Data.Migrations;

/// <summary>
/// Reads migrations embedded in an assembly. Files must be named "NNNN_description.sql",
/// e.g. "0001_initial.sql"; the number is the version.
/// </summary>
public sealed partial class EmbeddedMigrationSource(string module, Assembly assembly) : IMigrationSource
{
    [GeneratedRegex(@"(?<version>\d{4})_(?<name>[a-z0-9_]+)\.sql$", RegexOptions.IgnoreCase)]
    private static partial Regex FileName();

    public string Module { get; } = module;

    public IReadOnlyList<Migration> GetMigrations()
    {
        var migrations = new List<Migration>();
        foreach (var resource in assembly.GetManifestResourceNames())
        {
            var match = FileName().Match(resource);
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
