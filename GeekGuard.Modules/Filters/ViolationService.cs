using GeekGuard.Core.Messaging;
using GeekGuard.Modules.Moderation;

namespace GeekGuard.Modules.Filters;

/// <summary>
/// What every content filter does when a member breaks a rule: remove the message, then either warn the member
/// (the warning notice carries the reason) or post a short notice. Notices are throttled per member, so a
/// spammer cannot turn the bot's own replies into spam.
/// </summary>
public sealed class ViolationService(BotActions actions, WarningService warnings, NoticeThrottle throttle)
{
    private static readonly TimeSpan NoticeLifetime = TimeSpan.FromSeconds(30);

    private static readonly Localized Notice = new("{0}، {1}", "{0}, {1}");

    /// <param name="reason">Why the message was removed, e.g. «ارسال لینک در این گروه مجاز نیست».</param>
    /// <param name="warn">Also give the member a warning (counts toward the group's limit).</param>
    /// <param name="kind">Throttle bucket, usually the plugin id.</param>
    public async Task HandleAsync(GroupMessageContext context, Localized reason, bool warn, string kind, CancellationToken ct)
    {
        await actions.DeleteAsync(context.ChatId, context.Message.MessageId, ct);

        // A member posting as one of their channels has no user to warn or mention.
        if (context.Message is not { SenderChat: null, From: { } member }) return;

        var lang = context.Group.Lang;
        if (warn)
        {
            await warnings.WarnAsync(context.ChatId, lang, member, reason.Get(lang), ct);
            return;
        }

        if (throttle.TryAcquire(context.ChatId, member.Id, kind))
        {
            await actions.SendTemporaryAsync(context.ChatId, Notice.Format(lang, Html.Mention(member), reason.Get(lang)),
                NoticeLifetime, ct: ct);
        }
    }
}
