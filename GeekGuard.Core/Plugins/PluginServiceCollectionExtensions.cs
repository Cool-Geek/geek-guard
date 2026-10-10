using GeekGuard.Core.Callbacks;
using GeekGuard.Core.Data.Migrations;
using GeekGuard.Core.Panel;
using GeekGuard.Core.Private;
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

    /// <summary>Adds a handler for inline button presses; see <see cref="ICallbackHandler"/>.</summary>
    public static IServiceCollection AddCallbackHandler<THandler>(this IServiceCollection services)
        where THandler : class, ICallbackHandler
    {
        services.AddSingleton<ICallbackHandler, THandler>();
        return services;
    }

    /// <summary>Adds a handler for t.me/bot?start=… links; see <see cref="IStartLinkHandler"/>.</summary>
    public static IServiceCollection AddStartLinkHandler<THandler>(this IServiceCollection services)
        where THandler : class, IStartLinkHandler
    {
        services.AddSingleton<IStartLinkHandler, THandler>();
        return services;
    }

    /// <summary>Adds a section to every group's settings panel; see <see cref="IPanelSection"/>.</summary>
    public static IServiceCollection AddPanelSection<TSection>(this IServiceCollection services)
        where TSection : class, IPanelSection
    {
        services.AddSingleton<IPanelSection, TSection>();
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
