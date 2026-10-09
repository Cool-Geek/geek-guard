using CoolGeek.PersianText;
using GeekGuard.Core.Messaging;
using GeekGuard.Modules.Filters;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;

namespace GeekGuard.Modules.AntiLink;

/// <summary>
/// Removes links posted by members, including disguised ones like "t . me", links hidden behind text,
/// and link buttons. By default the member also gets a warning.
/// </summary>
public sealed class AntiLinkPlugin : IGeekGuardPlugin
{
    public const string Id = "anti-link";

    public PluginManifest Manifest { get; } = new(Id, "ضدلینک", "Anti-link", PluginTier.Free, EnabledByDefault: true);

    public void ConfigureServices(IServiceCollection services, IConfiguration configuration)
    {
        services.TryAddSingleton<ViolationService>();
        services.AddGroupMessageHandler<AntiLinkHandler>();
    }
}

/// <summary>Per-group anti-link settings.</summary>
public sealed class AntiLinkSettings
{
    /// <summary>Warn the member as well as removing the link.</summary>
    public bool WarnOnViolation { get; set; } = true;
}

public sealed class AntiLinkHandler(ViolationService violations, PluginStateStore states) : IGroupMessageHandler
{
    private static readonly Localized Reason = new("ارسال لینک در این گروه مجاز نیست.", "links are not allowed in this group.");

    public string PluginId => AntiLinkPlugin.Id;

    public int Order => HandlerOrder.ContentFilters;

    public async Task<HandlerResult> HandleAsync(GroupMessageContext context, CancellationToken ct)
    {
        if (context.SenderIsAdmin || !ContainsLink(context.Message, context.NormalizedText)) return HandlerResult.Continue;

        var settings = await states.GetSettingsAsync<AntiLinkSettings>(context.ChatId, AntiLinkPlugin.Id, ct);
        await violations.HandleAsync(context, Reason, settings.WarnOnViolation, AntiLinkPlugin.Id, ct);
        return HandlerResult.Stop;
    }

    /// <summary>Links Telegram marked up, link buttons under the message, or links hidden in the text.</summary>
    public static bool ContainsLink(Message message, NormalizedText text)
    {
        var entities = message.Entities ?? message.CaptionEntities ?? [];
        if (entities.Any(e => e.Type is MessageEntityType.Url or MessageEntityType.TextLink)) return true;

        if (message.ReplyMarkup?.InlineKeyboard.SelectMany(row => row).Any(button => button.Url is not null) == true)
            return true;

        return LinkDetector.ContainsLink(text);
    }
}
