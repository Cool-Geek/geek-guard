using GeekGuard.Core.Messaging;
using Telegram.Bot.Types.ReplyMarkups;

namespace GeekGuard.Modules.Reports;

/// <summary>The buttons under a report in an admin's private chat. Button data: "rp:&lt;action&gt;:&lt;report id&gt;".</summary>
public static class ReportButtons
{
    public const string Prefix = "rp";

    /// <summary>A press on the status button of a report that is already dealt with.</summary>
    public const string InfoVerb = "i";

    /// <summary>How long a member is muted from the report's button.</summary>
    public static readonly TimeSpan MuteFor = TimeSpan.FromHours(24);

    public static string Verb(ReportAction action) => action switch
    {
        ReportAction.Delete => "d",
        ReportAction.Mute => "m",
        ReportAction.Ban => "b",
        _ => "x",
    };

    public static ReportAction? Parse(string verb) => verb switch
    {
        "d" => ReportAction.Delete,
        "m" => ReportAction.Mute,
        "b" => ReportAction.Ban,
        "x" => ReportAction.Dismiss,
        _ => null,
    };

    /// <summary>Actions for an open report. Mute and ban only when the message has a member behind it.</summary>
    public static InlineKeyboardMarkup Open(long reportId, string lang, bool hasMember)
    {
        var fa = lang != Languages.English;
        InlineKeyboardButton Button(string fa_, string en, ReportAction action) =>
            InlineKeyboardButton.WithCallbackData(fa ? fa_ : en, $"{Prefix}:{Verb(action)}:{reportId}");

        var rows = new List<InlineKeyboardButton[]>
        {
            new[] { Button("🗑 حذف پیام", "🗑 Delete message", ReportAction.Delete), Button("✔️ بی‌مورد", "✔️ Dismiss", ReportAction.Dismiss) },
        };
        if (hasMember)
            rows.Add(new[] { Button("🔇 سکوت ۲۴ ساعت", "🔇 Mute 24h", ReportAction.Mute), Button("⛔ بن", "⛔ Ban", ReportAction.Ban) });
        return new InlineKeyboardMarkup(rows);
    }

    /// <summary>Replaces the actions once the report is dealt with: what was done and by whom.</summary>
    public static InlineKeyboardMarkup Handled(long reportId, string lang, ReportAction action, string adminName)
    {
        var fa = lang != Languages.English;
        var what = action switch
        {
            ReportAction.Delete => fa ? "🗑 پیام حذف شد" : "🗑 Message deleted",
            ReportAction.Mute => fa ? "🔇 ساکت شد" : "🔇 Muted",
            ReportAction.Ban => fa ? "⛔ بن شد" : "⛔ Banned",
            _ => fa ? "✔️ بی‌مورد" : "✔️ Dismissed",
        };
        var label = $"{what} — {Html.Truncate(adminName, 24)}";
        return new InlineKeyboardMarkup(InlineKeyboardButton.WithCallbackData(label, $"{Prefix}:{InfoVerb}:{reportId}"));
    }

    public static ReportAction? ParseStored(string? stored) =>
        Enum.TryParse<ReportAction>(stored, ignoreCase: true, out var action) ? action : null;
}
