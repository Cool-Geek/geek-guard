using GeekGuard.Core.Messaging;
using Telegram.Bot.Types;

namespace GeekGuard.Modules.Moderation;

/// <summary>
/// Gives a warning and applies the group's penalty once the limit is reached. Used by the /warn command
/// and, from the next step, by the filters when they remove a message.
/// </summary>
public sealed class WarningService(WarnStore warns, PluginStateStore states, BotActions actions)
{
    /// <summary>How long automatic warning notices stay in the chat.</summary>
    public static readonly TimeSpan NoticeLifetime = TimeSpan.FromSeconds(60);

    /// <param name="automatic">True when a filter warns; its notice deletes itself to keep the chat clean.</param>
    public async Task WarnAsync(long chatId, string lang, User member, string? reason, bool automatic, CancellationToken ct)
    {
        var settings = await states.GetSettingsAsync<ModerationSettings>(chatId, ModerationPlugin.Id, ct);
        var count = await warns.AddAsync(chatId, member.Id, ct);
        var mention = Html.Mention(member);

        if (count < settings.WarnLimit)
        {
            var warned = ModerationTexts.Warned.Format(lang, mention, count, settings.WarnLimit);
            if (!string.IsNullOrWhiteSpace(reason)) warned += "\n" + ModerationTexts.WarnReason.Format(lang, Html.Escape(reason));
            await Notify(chatId, warned, automatic, ct);
            return;
        }

        await warns.ResetAsync(chatId, member.Id, ct);

        var limit = settings.WarnLimit;
        var muteFor = TimeSpan.FromHours(settings.MuteHoursAtLimit);
        var (succeeded, text) = settings.ActionAtLimit switch
        {
            LimitAction.Ban => (await actions.BanAsync(chatId, member.Id, null, ct),
                ModerationTexts.WarnLimitBanned.Format(lang, mention, limit)),
            LimitAction.Kick => (await actions.KickAsync(chatId, member.Id, ct),
                ModerationTexts.WarnLimitKicked.Format(lang, mention, limit)),
            _ => (await actions.MuteAsync(chatId, member.Id, muteFor, ct),
                ModerationTexts.WarnLimitMuted.Format(lang, mention, limit, Duration.Format(muteFor, lang))),
        };

        await Notify(chatId, succeeded ? text : ModerationTexts.Failed.Get(lang), automatic: !succeeded, ct);
    }

    private Task Notify(long chatId, string html, bool automatic, CancellationToken ct) =>
        automatic ? actions.SendTemporaryAsync(chatId, html, NoticeLifetime, ct: ct) : actions.SendAsync(chatId, html, ct: ct);
}
