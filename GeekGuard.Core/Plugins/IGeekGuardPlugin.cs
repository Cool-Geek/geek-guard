namespace GeekGuard.Core.Plugins;

/// <summary>Whether a plugin is part of the free core or must be bought for a group.</summary>
public enum PluginTier
{
    Free = 0,
    Pro = 1,
}

/// <summary>Describes a plugin to the bot and, later, to admins in the settings panel.</summary>
/// <param name="Id">Stable identifier stored in the database, e.g. "anti-flood". Never change it after release.</param>
/// <param name="NameFa">Display name in Persian.</param>
/// <param name="NameEn">Display name in English.</param>
/// <param name="Tier">Free plugins work everywhere; Pro plugins need a licence for the group.</param>
/// <param name="EnabledByDefault">Whether the plugin is on in a group until an admin changes it.</param>
public sealed record PluginManifest(string Id, string NameFa, string NameEn, PluginTier Tier, bool EnabledByDefault);

/// <summary>
/// Entry point of a plugin. The bot finds every non-abstract implementation with a parameterless constructor,
/// in its built-in modules and in the assemblies under the plugins folder, and lets it register its services.
/// </summary>
/// <example>
/// <code>
/// public sealed class NightModePlugin : IGeekGuardPlugin
/// {
///     public PluginManifest Manifest { get; } = new("night-mode", "حالت شب", "Night mode", PluginTier.Pro, false);
///
///     public void ConfigureServices(IServiceCollection services, IConfiguration configuration)
///     {
///         services.AddGroupMessageHandler&lt;NightModeHandler&gt;();
///         services.AddPluginMigrations(Manifest.Id, GetType());
///     }
/// }
/// </code>
/// </example>
public interface IGeekGuardPlugin
{
    PluginManifest Manifest { get; }

    /// <summary>Registers the plugin's handlers, migrations and services. Called once at startup.</summary>
    void ConfigureServices(IServiceCollection services, IConfiguration configuration);
}
