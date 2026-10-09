using GeekGuard.Core.Messaging;
using Telegram.Bot.Types;

namespace GeekGuard.Modules.Moderation;

/// <summary>
/// Gives a warning and applies the group's penalty once the limit is reached. Used by the /warn command
/// and, from the next step, by the filters when they remove a message. Its notices delete themselves
/// after the group's cleanup delay.
/// </summary>
public sealed class WarningService(WarnStore warns, PluginStateStore states, BotActions actions)
{
    /// <summary>Returns false only when the penalty at the limit was refused by Telegram.</summary>
    public async Task<bool> WarnAsync(long chatId, string lang, User member, string? reason, CancellationToken ct)
    {
        var settings = await states.GetSettingsAsync<ModerationSettings>(chatId, ModerationPlugin.Id, ct);
        var count = await warns.AddAsync(chatId, member.Id, ct);
        var mention = Html.Mention(member);

        if (count < settings.WarnLimit)
        {
            var warned = ModerationTexts.Warned.Format(lang, mention, count, settings.WarnLimit);
            if (!string.IsNullOrWhiteSpace(reason)) warned += "\n" + ModerationTexts.WarnReason.Format(lang, Html.Escape(reason));
            await actions.SendTemporaryAsync(chatId, warned, settings.CleanupDelay, ct: ct);
            return true;
        }

        await warns.ResetAsync(chatId, member.Id, ct);

        var limit = settings.WarnLimit;
        mention = Html.MentionWithId(member); // the penalty may need undoing later
        var muteFor = TimeSpan.FromHours(settings.MuteHoursAtLimit);
        var (result, text) = settings.ActionAtLimit switch
        {
            LimitAction.Ban => (await actions.BanAsync(chatId, member.Id, null, ct),
                ModerationTexts.WarnLimitBanned.Format(lang, mention, limit)),
            LimitAction.Kick => (await actions.KickAsync(chatId, member.Id, ct),
                ModerationTexts.WarnLimitKicked.Format(lang, mention, limit)),
            _ => (await actions.MuteAsync(chatId, member.Id, muteFor, ct),
                ModerationTexts.WarnLimitMuted.Format(lang, mention, limit, Duration.Format(muteFor, lang))),
        };

        await actions.SendTemporaryAsync(chatId, result.Succeeded ? text : ModerationTexts.ForProblem(result, lang),
            result.Succeeded ? settings.CleanupDelay : TimeSpan.FromMinutes(1), ct: ct);
        return result.Succeeded;
    }
}
