using Telegram.Bot;

namespace GeekGuard.Modules.Diagnostics;

/// <summary>
/// /ping in a group: proves the bot is alive and shows which plugins run there.
/// Also the smallest complete example of a plugin.
/// </summary>
public sealed class DiagnosticsPlugin : IGeekGuardPlugin
{
    public const string Id = "diagnostics";

    public PluginManifest Manifest { get; } = new(Id, "عیب‌یابی", "Diagnostics", PluginTier.Free, EnabledByDefault: true);

    public void ConfigureServices(IServiceCollection services, IConfiguration configuration) =>
        services.AddGroupMessageHandler<PingHandler>();
}

public sealed class PingHandler(ITelegramBotClient bot, PluginCatalog catalog, PluginStateStore states) : IGroupMessageHandler
{
    public string PluginId => DiagnosticsPlugin.Id;

    public int Order => HandlerOrder.Commands;

    public async Task<HandlerResult> HandleAsync(GroupMessageContext context, CancellationToken ct)
    {
        if (context.Command?.Name != "ping") return HandlerResult.Continue;

        var version = typeof(IGeekGuardPlugin).Assembly.GetName().Version?.ToString(3) ?? "?";
        var lines = new List<string> { $"🏓 Pong! Geek Guard v{version}", "" };
        foreach (var plugin in catalog.All.OrderBy(p => p.Id))
        {
            var active = await states.IsActiveAsync(context.ChatId, plugin, ct);
            var name = context.Group.Lang == "en" ? plugin.NameEn : plugin.NameFa;
            lines.Add($"{(active ? "✅" : "⏸")} {name} ({plugin.Tier})");
        }

        await bot.SendMessage(context.ChatId, string.Join('\n', lines),
            replyParameters: context.Message.MessageId, cancellationToken: ct);
        return HandlerResult.Stop;
    }
}
