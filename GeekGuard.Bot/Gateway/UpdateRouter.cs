using GeekGuard.Core.Groups;
using GeekGuard.Core.Messaging;
using GeekGuard.Core.Users;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;

namespace GeekGuard.Bot.Gateway;

/// <summary>Sends each update to the part of the bot that handles it.</summary>
public sealed class UpdateRouter(
    ITelegramBotClient bot,
    UserRepository users,
    GroupDirectory groups,
    GroupMessagePipeline groupPipeline,
    ILogger<UpdateRouter> log)
{
    public Task RouteAsync(Update update, CancellationToken ct) => update switch
    {
        { Message: { Chat.Type: ChatType.Private } message } => HandlePrivateMessageAsync(message, ct),
        { Message: { Chat.Type: ChatType.Group or ChatType.Supergroup } message } => groupPipeline.RunAsync(message, ct),
        { MyChatMember: { } change } => HandleMyMembershipAsync(change, ct),
        _ => Task.CompletedTask,
    };

    /// <summary>Private chat. The settings panel replaces this in a later step.</summary>
    private async Task HandlePrivateMessageAsync(Message message, CancellationToken ct)
    {
        if (message is not { Text: { } text, From: { } from } || !text.StartsWith("/start", StringComparison.Ordinal))
            return;

        await users.UpsertAsync(from.Id, from.FirstName, from.Username, from.LanguageCode, ct);
        var total = await users.CountAsync(ct);
        log.LogInformation("/start from {UserId}; {Total} user(s) in the database", from.Id, total);

        await bot.SendMessage(message.Chat.Id,
            $"سلام! 👋 من Geek Guard هستم و آنلاینم ✅\nHi! 👋 I'm Geek Guard and I'm online ✅\n\n🗄 users: {total}",
            cancellationToken: ct);
    }

    /// <summary>The bot itself was added to or removed from a group.</summary>
    private async Task HandleMyMembershipAsync(ChatMemberUpdated change, CancellationToken ct)
    {
        if (change.Chat.Type is not (ChatType.Group or ChatType.Supergroup)) return;

        var isMember = change.NewChatMember.Status is ChatMemberStatus.Member or ChatMemberStatus.Administrator
            or ChatMemberStatus.Restricted;

        if (isMember)
        {
            await groups.GetOrAddAsync(change.Chat.Id, change.Chat.Title ?? "", change.From.Id,
                Languages.FromTelegram(change.From.LanguageCode), ct);
            log.LogInformation("Bot is in group {Chat} ({Title}) as {Status}",
                change.Chat.Id, change.Chat.Title, change.NewChatMember.Status);
        }
        else
        {
            await groups.DeactivateAsync(change.Chat.Id, ct);
            log.LogInformation("Bot left group {Chat} ({Title})", change.Chat.Id, change.Chat.Title);
        }
    }
}
