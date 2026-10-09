using GeekGuard.Core.Callbacks;
using GeekGuard.Core.Groups;
using GeekGuard.Core.Messaging;
using GeekGuard.Modules.Moderation;
using Telegram.Bot.Types;

namespace GeekGuard.Modules.Reports;

/// <summary>
/// Runs the buttons under a report: delete the message, mute or ban its author, or dismiss the report.
/// The first admin to press wins; every admin's copy then shows what was done and by whom.
/// </summary>
public sealed class ReportButtonHandler(
    ReportStore reports,
    GroupAdmins admins,
    MemberDirectory members,
    BotActions actions,
    PluginStateStore states) : ICallbackHandler
{
    private static readonly Localized NotAdmin = new(
        "⛔ دیگر ادمین این گروه نیستی.", "⛔ You are no longer an admin of that group.");
    private static readonly Localized NeedsRestrict = new(
        "⛔ برای سکوت و بن باید اجازه‌ی «محدود کردن اعضا» را داشته باشی.",
        "⛔ You need the “restrict members” right to mute or ban.");
    private static readonly Localized AlreadyHandled = new(
        "این گزارش قبلاً رسیدگی شده: {0}", "This report was already handled: {0}");
    private static readonly Localized Done = new("✅ انجام شد", "✅ Done");
    private static readonly Localized Gone = new("این گزارش دیگر وجود ندارد.", "This report no longer exists.");

    public string Prefix => ReportButtons.Prefix;

    public async Task HandleAsync(CallbackQuery query, string[] args, CancellationToken ct)
    {
        if (args.Length != 2 || !long.TryParse(args[1], out var reportId))
        {
            await actions.AnswerButtonAsync(query.Id, ct: ct);
            return;
        }

        var report = await reports.GetAsync(reportId, ct);
        if (report is null)
        {
            await actions.AnswerButtonAsync(query.Id, Gone.Get(Languages.Persian), ct: ct);
            return;
        }

        var lang = report.Lang;
        if (report.HandledAction is not null || args[0] == ReportButtons.InfoVerb)
        {
            await ShowHandledAsync(query, report, ct);
            return;
        }

        if (ReportButtons.Parse(args[0]) is not { } action)
        {
            await actions.AnswerButtonAsync(query.Id, ct: ct);
            return;
        }

        // Buttons sit in private chats and can be pressed days later: check the rights now, not when sent.
        var adminId = query.From.Id;
        if (!await admins.IsAdminAsync(report.ChatId, adminId, ct))
        {
            await actions.AnswerButtonAsync(query.Id, NotAdmin.Get(lang), alert: true, ct);
            return;
        }
        if (action is ReportAction.Mute or ReportAction.Ban && !await admins.CanRestrictAsync(report.ChatId, adminId, ct))
        {
            await actions.AnswerButtonAsync(query.Id, NeedsRestrict.Get(lang), alert: true, ct);
            return;
        }
        if (action is ReportAction.Mute or ReportAction.Ban
            && (report.AuthorId is not { } authorId || await admins.IsAdminAsync(report.ChatId, authorId, ct)))
        {
            await actions.AnswerButtonAsync(query.Id, ModerationTexts.CannotTargetAdmin.Get(lang), alert: true, ct);
            return;
        }

        var adminName = Html.DisplayName(query.From);
        var claimed = await reports.TryClaimAsync(reportId, adminId, adminName, action, ct);
        if (claimed is null)
        {
            // Another admin pressed first.
            await ShowHandledAsync(query, (await reports.GetAsync(reportId, ct))!, ct);
            return;
        }

        var result = await ExecuteAsync(claimed, action, ct);
        if (!result.Succeeded)
        {
            await reports.ReleaseAsync(reportId, ct);
            await actions.AnswerButtonAsync(query.Id, StripTags(ModerationTexts.ForProblem(result, lang)), alert: true, ct);
            return;
        }

        await actions.AnswerButtonAsync(query.Id, Done.Get(lang), ct: ct);
        var keyboard = ReportButtons.Handled(reportId, lang, action, adminName);
        foreach (var (deliveredTo, messageId) in await reports.GetDeliveriesAsync(reportId, ct))
            await actions.EditButtonsAsync(deliveredTo, messageId, keyboard, ct);
    }

    /// <summary>Carries out the action in the group. Punishments also remove the reported message.</summary>
    private async Task<ActionResult> ExecuteAsync(Report report, ReportAction action, CancellationToken ct)
    {
        switch (action)
        {
            case ReportAction.Dismiss:
                return ActionResult.Ok;

            case ReportAction.Delete:
                // Already gone (deleted by someone, or by a filter) counts as done.
                await actions.DeleteAsync(report.ChatId, report.MessageId, ct);
                return ActionResult.Ok;
        }

        var authorId = report.AuthorId!.Value;
        var result = action == ReportAction.Mute
            ? await actions.MuteAsync(report.ChatId, authorId, ReportButtons.MuteFor, ct)
            : await actions.BanAsync(report.ChatId, authorId, null, ct);
        if (!result.Succeeded) return result;

        await actions.DeleteAsync(report.ChatId, report.MessageId, ct);

        // Tell the group, the same way a mute or ban by command is announced.
        var author = (await members.FindAsync(report.ChatId, authorId, ct))?.ToUser()
                     ?? new User { Id = authorId, FirstName = authorId.ToString() };
        var tagged = Html.MentionWithId(author);
        var text = action == ReportAction.Mute
            ? ModerationTexts.MutedFor.Format(report.Lang, tagged, Duration.Format(ReportButtons.MuteFor, report.Lang))
            : ModerationTexts.Banned.Format(report.Lang, tagged);
        var settings = await states.GetSettingsAsync<ModerationSettings>(report.ChatId, ModerationPlugin.Id, ct);
        await actions.SendTemporaryAsync(report.ChatId, text, settings.CleanupDelay, ct: ct);
        return result;
    }

    /// <summary>Tells the admin who already dealt with the report, and puts the status on their copy too.</summary>
    private async Task ShowHandledAsync(CallbackQuery query, Report report, CancellationToken ct)
    {
        if (ReportButtons.ParseStored(report.HandledAction) is not { } done)
        {
            await actions.AnswerButtonAsync(query.Id, ct: ct);
            return;
        }

        var status = ReportButtons.Handled(report.Id, report.Lang, done, report.HandledByName ?? "?");
        await actions.AnswerButtonAsync(query.Id,
            AlreadyHandled.Format(report.Lang, status.InlineKeyboard.First().First().Text), ct: ct);
        if (query.Message is { } copy) await actions.EditButtonsAsync(copy.Chat.Id, copy.MessageId, status, ct);
    }

    /// <summary>Button answers are plain text of at most 200 characters; the problem messages are written for HTML.</summary>
    private static string StripTags(string html) =>
        Html.Truncate(System.Net.WebUtility.HtmlDecode(System.Text.RegularExpressions.Regex.Replace(html, "<[^>]+>", "")), 200);
}
