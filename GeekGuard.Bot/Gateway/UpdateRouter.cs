using GeekGuard.Bot.Help;
using GeekGuard.Bot.Panel;
using GeekGuard.Core.Callbacks;
using GeekGuard.Core.Groups;
using GeekGuard.Core.Private;
using GeekGuard.Core.Messaging;
using GeekGuard.Core.Users;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;

namespace GeekGuard.Bot.Gateway;

/// <summary>Sends each update to the part of the bot that handles it.</summary>
public sealed class UpdateRouter(
    UserRepository users,
    GroupDirectory groups,
    GroupAdmins admins,
    GroupMessagePipeline groupPipeline,
    BotActions actions,
    IEnumerable<ICallbackHandler> callbackHandlers,
    IEnumerable<IStartLinkHandler> startLinkHandlers,
    PanelNavigator panel,
    HelpNavigator help,
    ILogger<UpdateRouter> log)
{
    private readonly Dictionary<string, IStartLinkHandler> _startLinks =
        startLinkHandlers.ToDictionary(h => h.Prefix, StringComparer.Ordinal);

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

    /// <summary>Private chat: /start opens the settings panel; /start with a link parameter goes to its plugin.</summary>
    private async Task HandlePrivateMessageAsync(Message message, CancellationToken ct)
    {
        if (message is not { Text: { } text, From: { } from }) return;

        // Text the panel asked for (a welcome message, rules, a word to filter).
        if (!text.StartsWith('/'))
        {
            await panel.TryAcceptTextAsync(message, ct);
            return;
        }

        if (text.StartsWith("/help", StringComparison.Ordinal))
        {
            await users.UpsertAsync(from.Id, from.FirstName, from.Username, from.LanguageCode, ct);
            await help.ShowHomeAsync(message.Chat.Id, from, ct);
            return;
        }

        if (!text.StartsWith("/start", StringComparison.Ordinal) && !text.StartsWith("/cancel", StringComparison.Ordinal)) return;

        await users.UpsertAsync(from.Id, from.FirstName, from.Username, from.LanguageCode, ct);

        // "/start rules_-100123": a link from a group button, handled by the plugin that made it.
        var payload = text.Length > 7 ? text[7..].Trim() : "";
        var underscore = payload.IndexOf('_');
        if (underscore > 0 && _startLinks.TryGetValue(payload[..underscore], out var linkHandler))
        {
            await linkHandler.HandleAsync(message, payload[(underscore + 1)..], ct);
            return;
        }

        log.LogInformation("/start from {UserId}", from.Id);
        await panel.ShowHomeAsync(message.Chat.Id, from, ct);
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
