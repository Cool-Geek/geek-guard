using GeekGuard.Core.Data;
using GeekGuard.Core.Groups;
using GeekGuard.Core.Messaging;
using GeekGuard.Core.Plugins;

namespace GeekGuard.Core;

public static class CoreServiceCollectionExtensions
{
    /// <summary>Registers everything the bot and its plugins share: database, groups, plugin state.</summary>
    public static IServiceCollection AddGeekGuardCore(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddGeekGuardDatabase(configuration);

        services.AddSingleton(TimeProvider.System);
        services.AddSingleton<BotIdentity>();
        services.AddSingleton<GroupDirectory>();
        services.AddSingleton<GroupAdmins>();
        services.AddSingleton<MemberDirectory>();
        services.AddSingleton<BotActions>();
        services.AddSingleton<NoticeThrottle>();
        services.AddSingleton<PluginStateStore>();
        return services;
    }
}
