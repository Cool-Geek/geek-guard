using GeekGuard.Core.Data.Migrations;
using GeekGuard.Core.Pipeline;

namespace GeekGuard.Core.Plugins;

/// <summary>Helpers plugins use inside <see cref="IGeekGuardPlugin.ConfigureServices"/>.</summary>
public static class PluginServiceCollectionExtensions
{
    /// <summary>Adds a handler to the group message pipeline.</summary>
    public static IServiceCollection AddGroupMessageHandler<THandler>(this IServiceCollection services)
        where THandler : class, IGroupMessageHandler
    {
        services.AddSingleton<IGroupMessageHandler, THandler>();
        return services;
    }

    /// <summary>
    /// Registers the SQL migrations embedded next to <paramref name="anchor"/>: files in a "Migrations" folder
    /// beside the type, e.g. Modules/NightMode/Migrations/0001_initial.sql for NightMode.NightModePlugin.
    /// </summary>
    public static IServiceCollection AddPluginMigrations(this IServiceCollection services, string pluginId, Type anchor)
    {
        services.AddSingleton<IMigrationSource>(
            new EmbeddedMigrationSource(pluginId, anchor.Assembly, $"{anchor.Namespace}.Migrations"));
        return services;
    }
}
