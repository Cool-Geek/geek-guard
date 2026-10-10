using GeekGuard.Core.Messaging;
using GeekGuard.Core.Panel;
using Telegram.Bot.Types.ReplyMarkups;

namespace GeekGuard.Modules.Moderation;

/// <summary>Panel: warnings, the penalty at the limit, and cleanup after admin actions.</summary>
public sealed class ModerationSection(PluginStateStore states) : IPanelSection
{
    private static readonly Localized Text = new(
        "⚠️ <b>اخطار و مجازات</b>\n\n" +
        "سقف اخطار: <b>{0}</b>\n" +
        "در سقف اخطار: <b>{1}</b>\n" +
        "پاک شدن دستور ادمین و جواب ربات بعد از: <b>{2}</b>\n" +
        "پاک شدن پیام متخلف هنگام مجازات: <b>{3}</b>",
        "⚠️ <b>Warnings & penalties</b>\n\n" +
        "Warning limit: <b>{0}</b>\n" +
        "At the limit: <b>{1}</b>\n" +
        "Admin command and bot reply removed after: <b>{2}</b>\n" +
        "Remove the offending message on a penalty: <b>{3}</b>");

    private static readonly int[] MuteHourChoices = [1, 6, 24, 72, 168];
    private static readonly int[] CleanupChoices = [10, 30, 60, 300];

    public string Id => "mod";

    public string PluginId => ModerationPlugin.Id;

    public int Order => 20;

    public Localized Title { get; } = new("⚠️ اخطار و مجازات", "⚠️ Warnings");

    public bool HasPluginSwitch => false;

    public async Task<PanelView> ShowAsync(PanelContext context, CancellationToken ct)
    {
        var s = await states.GetSettingsAsync<ModerationSettings>(context.ChatId, ModerationPlugin.Id, ct);
        var lang = context.Lang;
        var fa = lang != Languages.English;

        var penalty = s.ActionAtLimit switch
        {
            LimitAction.Kick => fa ? "اخراج" : "kick",
            LimitAction.Ban => fa ? "بن" : "ban",
            _ => (fa ? "سکوت " : "mute for ") + Duration.Format(TimeSpan.FromHours(s.MuteHoursAtLimit), lang),
        };
        var html = context.T(Text).Replace("{0}", s.WarnLimit.ToString())
            .Replace("{1}", penalty)
            .Replace("{2}", Duration.Format(s.CleanupDelay, lang))
            .Replace("{3}", s.DeleteOffendingMessage ? "✅" : "❌");

        var rows = new List<InlineKeyboardButton[]>
        {
            new[]
            {
                PanelButtons.Action("➖", context, Id, "wl", "-"),
                PanelButtons.Open((fa ? "سقف اخطار: " : "Limit: ") + s.WarnLimit, context.ChatId, Id),
                PanelButtons.Action("➕", context, Id, "wl", "+"),
            },
            new[]
            {
                PanelButtons.Action(PanelButtons.Mark(s.ActionAtLimit == LimitAction.Mute, fa ? "🔇 سکوت" : "🔇 Mute"), context, Id, "act", "mute"),
                PanelButtons.Action(PanelButtons.Mark(s.ActionAtLimit == LimitAction.Kick, fa ? "👢 اخراج" : "👢 Kick"), context, Id, "act", "kick"),
                PanelButtons.Action(PanelButtons.Mark(s.ActionAtLimit == LimitAction.Ban, fa ? "⛔ بن" : "⛔ Ban"), context, Id, "act", "ban"),
            },
        };

        if (s.ActionAtLimit == LimitAction.Mute)
        {
            rows.Add(MuteHourChoices
                .Select(h => PanelButtons.Action(PanelButtons.Mark(s.MuteHoursAtLimit == h, Duration.Format(TimeSpan.FromHours(h), lang)),
                    context, Id, "mh", h.ToString()))
                .ToArray());
        }

        rows.Add(CleanupChoices
            .Select(c => PanelButtons.Action(PanelButtons.Mark(s.CleanupDelaySeconds == c, "🧹 " + Duration.Format(TimeSpan.FromSeconds(c), lang)),
                context, Id, "cl", c.ToString()))
            .ToArray());
        rows.Add(new[]
        {
            PanelButtons.Action((s.DeleteOffendingMessage ? "✅ " : "❌ ") + (fa ? "پاک شدن پیام متخلف" : "Remove offending message"),
                context, Id, "del"),
        });
        rows.Add(new[] { PanelButtons.BackToGroup(context) });
        return new PanelView(html, new InlineKeyboardMarkup(rows));
    }

    public async Task<string?> HandleAsync(PanelContext context, string[] args, CancellationToken ct)
    {
        var s = await states.GetSettingsAsync<ModerationSettings>(context.ChatId, ModerationPlugin.Id, ct);
        var updated = new ModerationSettings
        {
            WarnLimit = s.WarnLimit,
            ActionAtLimit = s.ActionAtLimit,
            MuteHoursAtLimit = s.MuteHoursAtLimit,
            CleanupDelaySeconds = s.CleanupDelaySeconds,
            DeleteOffendingMessage = s.DeleteOffendingMessage,
        };

        var value = args.Length > 1 ? args[1] : "";
        switch (args[0])
        {
            case "wl":
                updated.WarnLimit = Math.Clamp(s.WarnLimit + (value == "+" ? 1 : -1), 1, 10);
                break;
            case "act":
                updated.ActionAtLimit = value switch { "kick" => LimitAction.Kick, "ban" => LimitAction.Ban, _ => LimitAction.Mute };
                break;
            case "mh" when int.TryParse(value, out var hours) && MuteHourChoices.Contains(hours):
                updated.MuteHoursAtLimit = hours;
                break;
            case "cl" when int.TryParse(value, out var seconds) && CleanupChoices.Contains(seconds):
                updated.CleanupDelaySeconds = seconds;
                break;
            case "del":
                updated.DeleteOffendingMessage = !s.DeleteOffendingMessage;
                break;
            default:
                return null;
        }

        await states.SaveSettingsAsync(context.ChatId, ModerationPlugin.Id, updated, ct);
        return null;
    }
}
