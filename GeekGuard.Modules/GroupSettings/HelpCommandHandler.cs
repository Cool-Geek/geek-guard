using CoolGeek.PersianText;
using GeekGuard.Core.Messaging;
using GeekGuard.Core.Private;
using Telegram.Bot.Types.ReplyMarkups;

namespace GeekGuard.Modules.GroupSettings;

/// <summary>
/// /help or «راهنما» in a group (admins): a button that opens the full guide in the private chat,
/// where it can be as long as it needs without filling the group.
/// </summary>
public sealed class HelpCommandHandler(BotActions actions, BotIdentity me) : IGroupMessageHandler
{
    private static readonly string[] Keywords = new[] { "راهنما", "راهنمای ربات" }.Select(PersianNormalizer.Normalize).ToArray();

    private static readonly Localized Text = new(
        "📖 راهنمای کامل دستورها و تنظیمات در پیوی ربات است:",
        "📖 The full guide to commands and settings is in the bot's private chat:");
    private static readonly Localized Button = new("📖 باز کردن راهنما", "📖 Open the guide");

    public string PluginId => GroupSettingsPlugin.Id;

    public int Order => HandlerOrder.Commands;

    public async Task<HandlerResult> HandleAsync(GroupMessageContext context, CancellationToken ct)
    {
        var asked = context.Command is { Name: "help" } || Keywords.Contains(context.NormalizedText.Value);
        if (!asked || !context.SenderIsAdmin) return HandlerResult.Continue;

        var lang = context.Group.Lang;
        var keyboard = new InlineKeyboardMarkup(InlineKeyboardButton.WithUrl(Button.Get(lang), StartLinks.For(me.Username, "help", "0")));
        var sent = await actions.SendAsync(context.ChatId, Text.Get(lang), keyboard: keyboard, ct: ct);
        if (sent is not null) actions.ScheduleDelete(context.ChatId, sent.MessageId, TimeSpan.FromMinutes(1));
        actions.ScheduleDelete(context.ChatId, context.Message.MessageId, TimeSpan.FromMinutes(1));
        return HandlerResult.Stop;
    }
}
