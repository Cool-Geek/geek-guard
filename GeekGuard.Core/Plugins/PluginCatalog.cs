namespace GeekGuard.Core.Plugins;

/// <summary>Every plugin loaded in this process, by id.</summary>
public sealed class PluginCatalog
{
    private readonly Dictionary<string, PluginManifest> _byId;

    public PluginCatalog(IEnumerable<PluginManifest> manifests)
    {
        _byId = new Dictionary<string, PluginManifest>(StringComparer.Ordinal);
        foreach (var manifest in manifests)
        {
            if (!_byId.TryAdd(manifest.Id, manifest))
                throw new InvalidOperationException($"Two plugins use the id '{manifest.Id}'.");
        }
    }

    public IReadOnlyCollection<PluginManifest> All => _byId.Values;

    public PluginManifest Get(string id) =>
        _byId.TryGetValue(id, out var manifest)
            ? manifest
            : throw new InvalidOperationException($"No plugin with id '{id}' is loaded.");

    public bool Contains(string id) => _byId.ContainsKey(id);
}
