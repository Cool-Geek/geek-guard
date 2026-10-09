using GeekGuard.Core.Groups;
using GeekGuard.Core.Messaging;
using Telegram.Bot.Types;

namespace GeekGuard.Modules.Moderation;

/// <summary>Runs warn / mute / kick / ban commands for admins who are allowed to restrict members.</summary>
public sealed class ModerationHandler(
    GroupAdmins admins,
    BotActions actions,
    WarnStore warns,
    WarningService warnings,
    PluginStateStore states,
    BotIdentity me) : IGroupMessageHandler
{
    /// <summary>How long hints like "admins only" stay before deleting themselves.</summary>
    private static readonly TimeSpan HintLifetime = TimeSpan.FromSeconds(30);

    public string PluginId => ModerationPlugin.Id;

    public int Order => HandlerOrder.Commands;

    public async Task<HandlerResult> HandleAsync(GroupMessageContext context, CancellationToken ct)
    {
        var message = context.Message;
        var command = ModerationCommandParser.Parse(context.Command, context.NormalizedText);
        if (command is null) return HandlerResult.Continue;

        // Members get no reaction at all, not even "admins only": every bot reply to a member is a way
        // to make the bot flood the group. A keyword from a member is just chat, so it continues down the
        // pipeline; a slash command from a member is swallowed silently.
        if (!context.SenderIsAdmin)
            return command.IsSlashCommand ? HandlerResult.Stop : HandlerResult.Continue;

        // From here on the sender is an admin, so hints are safe: an admin writing "سکوت ۵ دقیقه" without
        // a reply clearly meant a command and is told how, instead of nothing happening.

        var lang = context.Group.Lang;
        var chatId = context.ChatId;

        if (!await IsAllowedAsync(context, ct))
        {
            // An admin without the "restrict members" right.
            await actions.SendTemporaryAsync(chatId, ModerationTexts.OnlyAdmins.Get(lang), HintLifetime, message.MessageId, ct);
            return HandlerResult.Stop;
        }

        if (command.InvalidDuration)
        {
            await actions.SendTemporaryAsync(chatId, ModerationTexts.InvalidDuration.Get(lang), HintLifetime, message.MessageId, ct);
            return HandlerResult.Stop;
        }

        var target = GetTarget(message);
        if (target is null)
        {
            await actions.SendTemporaryAsync(chatId, ModerationTexts.ReplyNeeded.Get(lang), HintLifetime, message.MessageId, ct);
            return HandlerResult.Stop;
        }

        if (target.Id == me.Id || await admins.IsAdminAsync(chatId, target.Id, ct))
        {
            await actions.SendTemporaryAsync(chatId, ModerationTexts.CannotTargetAdmin.Get(lang), HintLifetime, message.MessageId, ct);
            return HandlerResult.Stop;
        }

        var settings = await states.GetSettingsAsync<ModerationSettings>(chatId, ModerationPlugin.Id, ct);
        var succeeded = await ExecuteAsync(command, chatId, lang, target, settings.CleanupDelay, ct);

        // Clean up after the action: the admin's command and (via ExecuteAsync) the bot's reply always go;
        // the member's message goes too when it was punished, since it is usually the reason for the action.
        actions.ScheduleDelete(chatId, message.MessageId, settings.CleanupDelay);
        if (succeeded && IsPunishment(command.Action) && settings.DeleteOffendingMessage && message.ReplyToMessage is { } offending)
            actions.ScheduleDelete(chatId, offending.MessageId, settings.CleanupDelay);

        return HandlerResult.Stop;
    }

    private static bool IsPunishment(ModerationAction action) =>
        action is ModerationAction.Warn or ModerationAction.Mute or ModerationAction.Kick or ModerationAction.Ban;

    /// <summary>
    /// Anonymous admins are trusted: only admins can post as the group, and Telegram does not say which admin it was.
    /// Everyone else needs the live "restrict members" right.
    /// </summary>
    private async Task<bool> IsAllowedAsync(GroupMessageContext context, CancellationToken ct)
    {
        if (context.IsAnonymousAdmin) return true;
        if (context.Message.SenderChat is not null || context.Message.From is not { } from) return false;
        return await admins.CanRestrictAsync(context.ChatId, from.Id, ct);
    }

    /// <summary>The member being replied to. Messages sent as a channel or by the anonymous admin have no member.</summary>
    private static User? GetTarget(Message message) =>
        message.ReplyToMessage is { SenderChat: null, From: { } user } && user.Id != GroupAdmins.AnonymousAdminId
            ? user
            : null;

    /// <summary>Performs the action and announces it. Returns whether Telegram carried it out.</summary>
    private async Task<bool> ExecuteAsync(ModerationCommand command, long chatId, string lang, User target,
        TimeSpan cleanup, CancellationToken ct)
    {
        var mention = Html.Mention(target);
        switch (command.Action)
        {
            case ModerationAction.Warn:
                return await warnings.WarnAsync(chatId, lang, target, command.Reason, ct);

            case ModerationAction.Unwarn:
                var had = await warns.ResetAsync(chatId, target.Id, ct);
                await actions.SendTemporaryAsync(chatId,
                    (had > 0 ? ModerationTexts.Unwarned : ModerationTexts.NoWarnings).Format(lang, mention), cleanup, ct: ct);
                return true;

            case ModerationAction.Mute:
                return await ReportAsync(chatId, lang, await actions.MuteAsync(chatId, target.Id, command.Duration, ct),
                    command.Duration is { } md
                        ? ModerationTexts.MutedFor.Format(lang, mention, Duration.Format(md, lang))
                        : ModerationTexts.Muted.Format(lang, mention), cleanup, ct);

            case ModerationAction.Unmute:
                return await ReportAsync(chatId, lang, await actions.UnmuteAsync(chatId, target.Id, ct),
                    ModerationTexts.Unmuted.Format(lang, mention), cleanup, ct);

            case ModerationAction.Kick:
                return await ReportAsync(chatId, lang, await actions.KickAsync(chatId, target.Id, ct),
                    ModerationTexts.Kicked.Format(lang, mention), cleanup, ct);

            case ModerationAction.Ban:
                return await ReportAsync(chatId, lang, await actions.BanAsync(chatId, target.Id, command.Duration, ct),
                    command.Duration is { } bd
                        ? ModerationTexts.BannedFor.Format(lang, mention, Duration.Format(bd, lang))
                        : ModerationTexts.Banned.Format(lang, mention), cleanup, ct);

            case ModerationAction.Unban:
                return await ReportAsync(chatId, lang, await actions.UnbanAsync(chatId, target.Id, ct),
                    ModerationTexts.Unbanned.Format(lang, mention), cleanup, ct);

            default:
                return false;
        }
    }

    /// <summary>
    /// Announces the action (the announcement goes after the cleanup delay), or explains why Telegram
    /// refused it (that hint stays a minute so the admin has time to read it).
    /// </summary>
    private async Task<bool> ReportAsync(long chatId, string lang, ActionResult result, string successText,
        TimeSpan cleanup, CancellationToken ct)
    {
        if (result.Succeeded)
            await actions.SendTemporaryAsync(chatId, successText, cleanup, ct: ct);
        else
            await actions.SendTemporaryAsync(chatId, ModerationTexts.ForProblem(result, lang), TimeSpan.FromMinutes(1), ct: ct);
        return result.Succeeded;
    }
}
