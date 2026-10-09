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
    BotIdentity me) : IGroupMessageHandler
{
    /// <summary>How long hints like "admins only" stay before deleting themselves.</summary>
    private static readonly TimeSpan HintLifetime = TimeSpan.FromSeconds(30);

    public string PluginId => ModerationPlugin.Id;

    public int Order => HandlerOrder.Commands;

    public async Task<HandlerResult> HandleAsync(GroupMessageContext context, CancellationToken ct)
    {
        var message = context.Message;
        var command = ModerationCommandParser.Parse(context.Command, context.NormalizedText, message.ReplyToMessage is not null);
        if (command is null) return HandlerResult.Continue;

        // A member saying "بن" in a reply is just talking; only slash commands get an answer.
        if (!command.IsSlashCommand && !context.SenderIsAdmin) return HandlerResult.Continue;

        var lang = context.Group.Lang;
        var chatId = context.ChatId;

        if (!await IsAllowedAsync(context, ct))
        {
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

        // The command did its job; removing it keeps the group readable.
        await actions.DeleteAsync(chatId, message.MessageId, ct);
        await ExecuteAsync(command, chatId, lang, target, ct);
        return HandlerResult.Stop;
    }

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

    private async Task ExecuteAsync(ModerationCommand command, long chatId, string lang, User target, CancellationToken ct)
    {
        var mention = Html.Mention(target);
        switch (command.Action)
        {
            case ModerationAction.Warn:
                await warnings.WarnAsync(chatId, lang, target, command.Reason, automatic: false, ct);
                return;

            case ModerationAction.Unwarn:
                var had = await warns.ResetAsync(chatId, target.Id, ct);
                await actions.SendAsync(chatId, (had > 0 ? ModerationTexts.Unwarned : ModerationTexts.NoWarnings).Format(lang, mention), ct: ct);
                return;

            case ModerationAction.Mute:
                await ReportAsync(chatId, lang, await actions.MuteAsync(chatId, target.Id, command.Duration, ct),
                    command.Duration is { } md
                        ? ModerationTexts.MutedFor.Format(lang, mention, Duration.Format(md, lang))
                        : ModerationTexts.Muted.Format(lang, mention), ct);
                return;

            case ModerationAction.Unmute:
                await ReportAsync(chatId, lang, await actions.UnmuteAsync(chatId, target.Id, ct),
                    ModerationTexts.Unmuted.Format(lang, mention), ct);
                return;

            case ModerationAction.Kick:
                await ReportAsync(chatId, lang, await actions.KickAsync(chatId, target.Id, ct),
                    ModerationTexts.Kicked.Format(lang, mention), ct);
                return;

            case ModerationAction.Ban:
                await ReportAsync(chatId, lang, await actions.BanAsync(chatId, target.Id, command.Duration, ct),
                    command.Duration is { } bd
                        ? ModerationTexts.BannedFor.Format(lang, mention, Duration.Format(bd, lang))
                        : ModerationTexts.Banned.Format(lang, mention), ct);
                return;

            case ModerationAction.Unban:
                await ReportAsync(chatId, lang, await actions.UnbanAsync(chatId, target.Id, ct),
                    ModerationTexts.Unbanned.Format(lang, mention), ct);
                return;
        }
    }

    private Task ReportAsync(long chatId, string lang, bool succeeded, string successText, CancellationToken ct) =>
        succeeded
            ? actions.SendAsync(chatId, successText, ct: ct)
            : actions.SendTemporaryAsync(chatId, ModerationTexts.Failed.Get(lang), HintLifetime, ct: ct);
}
