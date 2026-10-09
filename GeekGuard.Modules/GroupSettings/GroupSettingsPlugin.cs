using GeekGuard.Core.Groups;
using GeekGuard.Core.Messaging;

namespace GeekGuard.Modules.GroupSettings;

/// <summary>
/// Group-wide settings admins change from inside the group. For now: the language of the bot's messages
/// (/lang fa, /lang en, or the keywords «زبان فارسی» / «زبان انگلیسی»). The private-chat panel takes over later.
/// </summary>
public sealed class GroupSettingsPlugin : IGeekGuardPlugin
{
    public const string Id = "group-settings";

    public PluginManifest Manifest { get; } = new(Id, "تنظیمات گروه", "Group settings", PluginTier.Free, EnabledByDefault: true);

    public void ConfigureServices(IServiceCollection services, IConfiguration configuration) =>
        services.AddGroupMessageHandler<LanguageHandler>();
}

public sealed class LanguageHandler(GroupDirectory groups, BotActions actions) : IGroupMessageHandler
{
    private static readonly Localized Changed = new("✅ زبان پیام‌های ربات در این گروه: فارسی", "✅ Bot messages in this group are now in English.");
    private static readonly Localized Usage = new("زبان را این‌طور بنویس: /lang fa یا /lang en", "Usage: /lang fa or /lang en");

    public string PluginId => GroupSettingsPlugin.Id;

    public int Order => HandlerOrder.Commands;

    public async Task<HandlerResult> HandleAsync(GroupMessageContext context, CancellationToken ct)
    {
        var requested = ReadRequest(context);
        if (requested is null) return HandlerResult.Continue;

        // Only admins change group settings. Members get no answer (no spam), and their message carries on
        // to the filters.
        if (!context.SenderIsAdmin) return HandlerResult.Continue;

        if (requested == "")
        {
            await actions.SendAsync(context.ChatId, Usage.Get(context.Group.Lang), context.Message.MessageId, ct: ct);
            return HandlerResult.Stop;
        }

        await groups.SetLangAsync(context.ChatId, requested, ct);
        await actions.SendAsync(context.ChatId, Changed.Get(requested), ct: ct);
        return HandlerResult.Stop;
    }

    /// <summary>The requested language, "" for a /lang with no valid argument, or null if this is not a language request.</summary>
    private static string? ReadRequest(GroupMessageContext context)
    {
        if (context.Command is { Name: "lang" or "language" } command)
        {
            return command.Args.Trim().ToLowerInvariant() switch
            {
                "fa" or "فارسی" => Languages.Persian,
                "en" or "english" or "انگلیسی" => Languages.English,
                _ => "",
            };
        }

        return context.NormalizedText.Value switch
        {
            "زبان فارسی" => Languages.Persian,
            "زبان انگلیسی" or "language english" => Languages.English,
            _ => null,
        };
    }
}
