using GeekGuard.Core.Groups;
using GeekGuard.Core.Messaging;

namespace GeekGuard.Modules.Diagnostics;

/// <summary>
/// /ping for admins: proves the bot is alive, shows whether it has the rights it needs,
/// and lists which plugins run in the group. Also the smallest complete example of a plugin.
/// </summary>
public sealed class DiagnosticsPlugin : IGeekGuardPlugin
{
    public const string Id = "diagnostics";

    public PluginManifest Manifest { get; } = new(Id, "عیب‌یابی", "Diagnostics", PluginTier.Free, EnabledByDefault: true);

    public void ConfigureServices(IServiceCollection services, IConfiguration configuration)
    {
        services.AddGroupMessageHandler<PingHandler>();
        services.AddHelpTopic(Help.HelpTopics.Diagnostics);
    }
}

public sealed class PingHandler(
    BotActions actions,
    GroupAdmins admins,
    BotIdentity me,
    PluginCatalog catalog,
    PluginStateStore states) : IGroupMessageHandler
{
    private static readonly Localized Rights = new("دسترسی‌های من:", "My rights:");
    private static readonly Localized CanDelete = new("حذف پیام", "Delete messages");
    private static readonly Localized CanRestrict = new("محدود کردن اعضا", "Restrict members");
    private static readonly Localized Plugins = new("پلاگین‌ها:", "Plugins:");

    public string PluginId => DiagnosticsPlugin.Id;

    public int Order => HandlerOrder.Commands;

    public async Task<HandlerResult> HandleAsync(GroupMessageContext context, CancellationToken ct)
    {
        if (context.Command?.Name != "ping") return HandlerResult.Continue;

        // Members get silence (no flooding with bot replies), and their message carries on to the filters.
        if (!context.SenderIsAdmin) return HandlerResult.Continue;

        var lang = context.Group.Lang;
        var version = typeof(IGeekGuardPlugin).Assembly.GetName().Version?.ToString(3) ?? "?";
        var (canDelete, canRestrict) = await admins.GetBotRightsAsync(context.ChatId, me.Id, ct);

        var lines = new List<string>
        {
            $"🏓 <b>Pong!</b> Geek Guard v{version}",
            "",
            $"<b>{Rights.Get(lang)}</b>",
            $"{Mark(canDelete)} {CanDelete.Get(lang)}",
            $"{Mark(canRestrict)} {CanRestrict.Get(lang)}",
            "",
            $"<b>{Plugins.Get(lang)}</b>",
        };

        foreach (var plugin in catalog.All.OrderBy(p => p.Id))
        {
            var active = await states.IsActiveAsync(context.ChatId, plugin, ct);
            var name = lang == Languages.English ? plugin.NameEn : plugin.NameFa;
            lines.Add($"{(active ? "✅" : "⏸")} {Html.Escape(name)} ({plugin.Tier})");
        }

        await actions.SendTemporaryAsync(context.ChatId, string.Join('\n', lines), TimeSpan.FromMinutes(1), context.Message.MessageId, ct);
        return HandlerResult.Stop;
    }

    private static string Mark(bool ok) => ok ? "✅" : "❌";
}
