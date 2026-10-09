using GeekGuard.Core.Callbacks;
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
    GroupAdmins admins,
    GroupMessagePipeline groupPipeline,
    BotActions actions,
    IEnumerable<ICallbackHandler> callbackHandlers,
    ILogger<UpdateRouter> log)
{
    private readonly Dictionary<string, ICallbackHandler> _callbacks =
        callbackHandlers.ToDictionary(h => h.Prefix, StringComparer.Ordinal);

    public Task RouteAsync(Update update, CancellationToken ct) => update switch
    {
        { Message: { Chat.Type: ChatType.Private } message } => HandlePrivateMessageAsync(message, ct),
        { Message: { Chat.Type: ChatType.Group or ChatType.Supergroup } message } => groupPipeline.RunAsync(message, ct),
        { EditedMessage: { Chat.Type: ChatType.Group or ChatType.Supergroup } edited } => groupPipeline.RunAsync(edited, ct, isEdit: true),
        { CallbackQuery: { } query } => HandleButtonAsync(query, ct),
        { MyChatMember: { } change } => HandleMyMembershipAsync(change, ct),
        { ChatMember: { } change } => HandleMemberChangedAsync(change),
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

    /// <summary>An inline button was pressed: hand it to the plugin that owns the button's prefix.</summary>
    private async Task HandleButtonAsync(CallbackQuery query, CancellationToken ct)
    {
        var parts = (query.Data ?? "").Split(':');
        if (!_callbacks.TryGetValue(parts[0], out var handler))
        {
            // Buttons from a plugin that was removed, or forged data: just stop the spinner.
            await actions.AnswerButtonAsync(query.Id, ct: ct);
            return;
        }
        await handler.HandleAsync(query, parts[1..], ct);
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
                Languages.ForNewGroup(change.Chat.Title, change.From.LanguageCode), ct);
            await admins.RefreshAsync(change.Chat.Id, ct);
            log.LogInformation("Bot is in group {Chat} ({Title}) as {Status}",
                change.Chat.Id, change.Chat.Title, change.NewChatMember.Status);
        }
        else
        {
            await groups.DeactivateAsync(change.Chat.Id, ct);
            log.LogInformation("Bot left group {Chat} ({Title})", change.Chat.Id, change.Chat.Title);
        }
    }

    /// <summary>Someone else's status changed (needs the bot to be an admin to receive these).</summary>
    private Task HandleMemberChangedAsync(ChatMemberUpdated change)
    {
        admins.OnMemberChanged(change);
        return Task.CompletedTask;
    }
}
