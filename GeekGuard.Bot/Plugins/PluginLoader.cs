using System.Reflection;
using GeekGuard.Core.Plugins;

namespace GeekGuard.Bot.Plugins;

/// <summary>
/// Finds plugins and lets each register its services. Two sources:
/// built-in assemblies (the free modules, referenced at compile time) and
/// the plugins folder: plugins/&lt;Name&gt;/&lt;Name&gt;.dll, which is how Pro plugins are deployed.
/// </summary>
public static class PluginLoader
{
    public const string DirectorySetting = "Plugins:Directory";

    public static PluginCatalog AddGeekGuardPlugins(this IServiceCollection services, IConfiguration configuration,
        ILogger log, params Assembly[] builtIn)
    {
        var assemblies = new List<Assembly>(builtIn);
        assemblies.AddRange(LoadExternalAssemblies(configuration, log));

        var plugins = assemblies
            .SelectMany(GetLoadableTypes)
            .Where(t => typeof(IGeekGuardPlugin).IsAssignableFrom(t) && t is { IsAbstract: false, IsInterface: false }
                        && t.GetConstructor(Type.EmptyTypes) is not null)
            .Select(t => (IGeekGuardPlugin)Activator.CreateInstance(t)!)
            .OrderBy(p => p.Manifest.Id, StringComparer.Ordinal)
            .ToList();

        // Throws on duplicate ids before anything is registered.
        var catalog = new PluginCatalog(plugins.Select(p => p.Manifest));

        foreach (var plugin in plugins)
        {
            plugin.ConfigureServices(services, configuration);
            log.LogInformation("Plugin loaded: {Id} ({Tier}, {Default})", plugin.Manifest.Id, plugin.Manifest.Tier,
                plugin.Manifest.EnabledByDefault ? "on by default" : "off by default");
        }

        services.AddSingleton(catalog);
        return catalog;
    }

    private static IEnumerable<Assembly> LoadExternalAssemblies(IConfiguration configuration, ILogger log)
    {
        var directory = Path.Combine(AppContext.BaseDirectory, configuration[DirectorySetting] ?? "plugins");
        if (!Directory.Exists(directory)) yield break;

        foreach (var folder in Directory.GetDirectories(directory).Order(StringComparer.Ordinal))
        {
            var dll = Path.Combine(folder, Path.GetFileName(folder) + ".dll");
            if (!File.Exists(dll))
            {
                log.LogWarning("Skipping plugin folder {Folder}: expected {Dll}", folder, Path.GetFileName(dll));
                continue;
            }

            log.LogInformation("Loading external plugin assembly {Dll}", dll);
            yield return new PluginLoadContext(dll).LoadFromAssemblyPath(dll);
        }
    }

    /// <summary>Types of an assembly, tolerating ones whose dependencies are missing.</summary>
    private static IEnumerable<Type> GetLoadableTypes(Assembly assembly)
    {
        try
        {
            return assembly.GetTypes();
        }
        catch (ReflectionTypeLoadException ex)
        {
            return ex.Types.Where(t => t is not null)!;
        }
    }
}
