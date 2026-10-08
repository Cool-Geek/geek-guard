using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;

namespace GeekGuard.Bot.Gateway;

/// <summary>
/// Decides what handles each update. For now it only answers /start in private chats;
/// group moderation and the settings panel plug in here in later steps.
/// </summary>
public sealed class UpdateRouter(ITelegramBotClient bot, ILogger<UpdateRouter> log)
{
    public async Task RouteAsync(Update update, CancellationToken ct)
    {
        if (update.Message is { Chat.Type: ChatType.Private, Text: { } text } message
            && text.StartsWith("/start", StringComparison.Ordinal))
        {
            log.LogInformation("/start from {UserId}", message.From?.Id);
            await bot.SendMessage(message.Chat.Id,
                "سلام! 👋 من Geek Guard هستم و آنلاینم ✅\nHi! 👋 I'm Geek Guard and I'm online ✅",
                cancellationToken: ct);
        }
    }
}
