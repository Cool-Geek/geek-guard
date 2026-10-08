using System.Reflection;
using System.Runtime.Loader;

namespace GeekGuard.Bot.Plugins;

/// <summary>
/// Loads one external plugin with its own dependencies, while sharing everything the host already has
/// (GeekGuard.Core, Telegram.Bot, Npgsql…). Sharing matters: a plugin's IGeekGuardPlugin must be the very
/// same type the host knows, or the host would not recognise it.
/// </summary>
internal sealed class PluginLoadContext(string pluginPath) : AssemblyLoadContext(Path.GetFileNameWithoutExtension(pluginPath))
{
    private readonly AssemblyDependencyResolver _resolver = new(pluginPath);

    protected override Assembly? Load(AssemblyName assemblyName)
    {
        // Prefer the host's copy of any assembly it can resolve.
        try
        {
            return Default.LoadFromAssemblyName(assemblyName);
        }
        catch (FileNotFoundException)
        {
            // Not part of the host: fall through to the plugin's own folder.
        }

        var path = _resolver.ResolveAssemblyToPath(assemblyName);
        return path is null ? null : LoadFromAssemblyPath(path);
    }

    protected override IntPtr LoadUnmanagedDll(string unmanagedDllName)
    {
        var path = _resolver.ResolveUnmanagedDllToPath(unmanagedDllName);
        return path is null ? IntPtr.Zero : LoadUnmanagedDllFromPath(path);
    }
}
