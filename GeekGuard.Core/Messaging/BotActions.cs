using Telegram.Bot;
using Telegram.Bot.Exceptions;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types.ReplyMarkups;

namespace GeekGuard.Core.Messaging;

/// <summary>
/// The Bot API calls moderation needs, with the error handling they all share: a call that Telegram refuses
/// (missing rights, basic group, user left…) returns the reason instead of throwing.
/// </summary>
public sealed class BotActions(ITelegramBotClient bot, ILogger<BotActions> log)
{
    private static readonly LinkPreviewOptions NoPreview = new() { IsDisabled = true };

    /// <summary>Sends an HTML message. Returns null if Telegram refused it.</summary>
    public async Task<Message?> SendAsync(long chatId, string html, int? replyTo = null,
        InlineKeyboardMarkup? keyboard = null, CancellationToken ct = default)
    {
        try
        {
            return await bot.SendMessage(chatId, html, ParseMode.Html,
                replyParameters: replyTo is { } id ? new ReplyParameters { MessageId = id, AllowSendingWithoutReply = true } : null,
                replyMarkup: keyboard, linkPreviewOptions: NoPreview, cancellationToken: ct);
        }
        catch (ApiRequestException ex)
        {
            log.LogDebug("sendMessage to {Chat} refused: {Message}", chatId, ex.Message);
            return null;
        }
    }

    /// <summary>Number of members in a chat, or null if Telegram would not say.</summary>
    public async Task<int?> GetMemberCountAsync(long chatId, CancellationToken ct = default)
    {
        try
        {
            return await bot.GetChatMemberCount(chatId, ct);
        }
        catch (ApiRequestException ex)
        {
            log.LogDebug("getChatMemberCount in {Chat} refused: {Message}", chatId, ex.Message);
            return null;
        }
    }

    /// <summary>Answers a button press with a short toast, or a pop-up the user must close when <paramref name="alert"/>.</summary>
    public async Task AnswerButtonAsync(string queryId, string? text = null, bool alert = false, CancellationToken ct = default)
    {
        try
        {
            await bot.AnswerCallbackQuery(queryId, text, alert, cancellationToken: ct);
        }
        catch (ApiRequestException ex)
        {
            // Presses older than a few minutes can no longer be answered; nothing to do about it.
            log.LogDebug("answerCallbackQuery refused: {Message}", ex.Message);
        }
    }

    /// <summary>Replaces the buttons under a message. Returns false if Telegram refused (e.g. message deleted).</summary>
    public async Task<bool> EditButtonsAsync(long chatId, int messageId, InlineKeyboardMarkup? keyboard, CancellationToken ct = default)
    {
        try
        {
            await bot.EditMessageReplyMarkup(chatId, messageId, keyboard, cancellationToken: ct);
            return true;
        }
        catch (ApiRequestException ex)
        {
            log.LogDebug("editMessageReplyMarkup in {Chat} refused: {Message}", chatId, ex.Message);
            return false;
        }
    }

    /// <summary>Sends a notice that deletes itself after <paramref name="lifetime"/>, keeping the group tidy.</summary>
    public async Task SendTemporaryAsync(long chatId, string html, TimeSpan lifetime, int? replyTo = null,
        CancellationToken ct = default)
    {
        var message = await SendAsync(chatId, html, replyTo, ct: ct);
        if (message is not null) ScheduleDelete(chatId, message.MessageId, lifetime);
    }

    /// <summary>
    /// Deletes a message after <paramref name="delay"/>, without waiting for it.
    /// The schedule lives in memory: if the bot restarts in between, the message simply stays.
    /// </summary>
    public void ScheduleDelete(long chatId, int messageId, TimeSpan delay) =>
        _ = Task.Run(async () =>
        {
            await Task.Delay(delay);
            await DeleteAsync(chatId, messageId, CancellationToken.None);
        });

    public async Task<bool> DeleteAsync(long chatId, int messageId, CancellationToken ct = default)
    {
        try
        {
            await bot.DeleteMessage(chatId, messageId, ct);
            return true;
        }
        catch (ApiRequestException ex)
        {
            log.LogDebug("deleteMessage in {Chat} refused: {Message}", chatId, ex.Message);
            return false;
        }
    }

    /// <summary>Mutes a member. A null <paramref name="duration"/> mutes until an admin unmutes.</summary>
    public async Task<ActionResult> MuteAsync(long chatId, long userId, TimeSpan? duration, CancellationToken ct = default)
    {
        try
        {
            await bot.RestrictChatMember(chatId, userId, NoPermissions(), untilDate: Until(duration), cancellationToken: ct);
            return ActionResult.Ok;
        }
        catch (ApiRequestException ex)
        {
            log.LogWarning("restrictChatMember in {Chat} refused: {Message}", chatId, ex.Message);
            return ActionResult.From(ex);
        }
    }

    public async Task<ActionResult> UnmuteAsync(long chatId, long userId, CancellationToken ct = default)
    {
        try
        {
            // Telegram: passing every permission as true lifts the restriction; the group's defaults still apply.
            await bot.RestrictChatMember(chatId, userId, AllPermissions(), cancellationToken: ct);
            return ActionResult.Ok;
        }
        catch (ApiRequestException ex)
        {
            log.LogWarning("unmute in {Chat} refused: {Message}", chatId, ex.Message);
            return ActionResult.From(ex);
        }
    }

    /// <summary>Bans a member. A null <paramref name="duration"/> bans until an admin unbans.</summary>
    public async Task<ActionResult> BanAsync(long chatId, long userId, TimeSpan? duration, CancellationToken ct = default)
    {
        try
        {
            await bot.BanChatMember(chatId, userId, untilDate: Until(duration), cancellationToken: ct);
            return ActionResult.Ok;
        }
        catch (ApiRequestException ex)
        {
            log.LogWarning("banChatMember in {Chat} refused: {Message}", chatId, ex.Message);
            return ActionResult.From(ex);
        }
    }

    /// <summary>
    /// Removes a member who may rejoin later. Telegram has no kick call: the usual way is a ban
    /// followed at once by an unban, which lifts the ban without bringing the member back.
    /// </summary>
    public async Task<ActionResult> KickAsync(long chatId, long userId, CancellationToken ct = default)
    {
        try
        {
            await bot.BanChatMember(chatId, userId, cancellationToken: ct);
            await bot.UnbanChatMember(chatId, userId, onlyIfBanned: true, cancellationToken: ct);
            return ActionResult.Ok;
        }
        catch (ApiRequestException ex)
        {
            log.LogWarning("kick in {Chat} refused: {Message}", chatId, ex.Message);
            return ActionResult.From(ex);
        }
    }

    public async Task<ActionResult> UnbanAsync(long chatId, long userId, CancellationToken ct = default)
    {
        try
        {
            // onlyIfBanned: without it, unbanning a current member would kick them out of the group.
            await bot.UnbanChatMember(chatId, userId, onlyIfBanned: true, cancellationToken: ct);
            return ActionResult.Ok;
        }
        catch (ApiRequestException ex)
        {
            log.LogWarning("unbanChatMember in {Chat} refused: {Message}", chatId, ex.Message);
            return ActionResult.From(ex);
        }
    }

    /// <summary>Telegram treats an until-date under 30 seconds or over 366 days as "forever".</summary>
    private static DateTime? Until(TimeSpan? duration) => duration is { } d ? DateTime.UtcNow + d : null;

    // Built with property initialisers rather than the bool constructor, which not every Telegram.Bot 22.x has.
    private static ChatPermissions NoPermissions() => new()
    {
        CanSendMessages = false, CanSendAudios = false, CanSendDocuments = false, CanSendPhotos = false,
        CanSendVideos = false, CanSendVideoNotes = false, CanSendVoiceNotes = false, CanSendPolls = false,
        CanSendOtherMessages = false, CanAddWebPagePreviews = false,
    };

    private static ChatPermissions AllPermissions() => new()
    {
        CanSendMessages = true, CanSendAudios = true, CanSendDocuments = true, CanSendPhotos = true,
        CanSendVideos = true, CanSendVideoNotes = true, CanSendVoiceNotes = true, CanSendPolls = true,
        CanSendOtherMessages = true, CanAddWebPagePreviews = true, CanChangeInfo = true, CanInviteUsers = true,
        CanPinMessages = true, CanManageTopics = true,
    };
}
