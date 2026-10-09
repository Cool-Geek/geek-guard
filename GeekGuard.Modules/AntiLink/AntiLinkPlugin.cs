using CoolGeek.PersianText;
using GeekGuard.Core.Messaging;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;

namespace GeekGuard.Modules.AntiLink;

/// <summary>Removes links posted by members, including disguised ones like "t . me" and link buttons.</summary>
public sealed class AntiLinkPlugin : IGeekGuardPlugin
{
    public const string Id = "anti-link";

    public PluginManifest Manifest { get; } = new(Id, "ضدلینک", "Anti-link", PluginTier.Free, EnabledByDefault: true);

    public void ConfigureServices(IServiceCollection services, IConfiguration configuration) =>
        services.AddGroupMessageHandler<AntiLinkHandler>();
}

public sealed class AntiLinkHandler(BotActions actions, NoticeThrottle throttle) : IGroupMessageHandler
{
    private static readonly TimeSpan NoticeLifetime = TimeSpan.FromSeconds(30);

    private static readonly Localized LinkRemoved = new(
        "🔗 {0}، ارسال لینک در این گروه مجاز نیست.",
        "🔗 {0}, links are not allowed in this group.");

    public string PluginId => AntiLinkPlugin.Id;

    public int Order => HandlerOrder.ContentFilters;

    public async Task<HandlerResult> HandleAsync(GroupMessageContext context, CancellationToken ct)
    {
        if (context.SenderIsAdmin || !ContainsLink(context.Message, context.NormalizedText)) return HandlerResult.Continue;

        await actions.DeleteAsync(context.ChatId, context.Message.MessageId, ct);

        // Members posting as one of their channels have no user to mention. The throttle keeps a link
        // spammer from turning the bot's own notices into spam: one notice per member per minute.
        if (context.Message is { SenderChat: null, From: { } from }
            && throttle.TryAcquire(context.ChatId, from.Id, AntiLinkPlugin.Id))
        {
            await actions.SendTemporaryAsync(context.ChatId, LinkRemoved.Format(context.Group.Lang, Html.Mention(from)),
                NoticeLifetime, ct: ct);
        }
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
