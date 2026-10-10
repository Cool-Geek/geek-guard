using GeekGuard.Core.Messaging;
using GeekGuard.Core.Panel;
using GeekGuard.Modules.Moderation;
using Telegram.Bot.Types.ReplyMarkups;

namespace GeekGuard.Modules.AntiFlood;

/// <summary>Panel: anti-flood level and how long flooders are muted.</summary>
public sealed class AntiFloodSection(PluginStateStore states) : IPanelSection
{
    private static readonly Localized Text = new(
        "🌊 <b>ضدفلود</b>\n\nاگر کسی در ۱۰ ثانیه بیش از حد پیام بفرستد، همه‌ی آن پیام‌ها پاک و خودش ساکت می‌شود.\n\nسطح: <b>{0}</b>\nمدت سکوت: <b>{1}</b>",
        "🌊 <b>Anti-flood</b>\n\nWhoever sends too many messages within 10 seconds has them all removed and is muted.\n\nLevel: <b>{0}</b>\nMute for: <b>{1}</b>");

    private static readonly int[] MuteChoices = [5, 10, 30, 60];

    public string Id => "flood";

    public string PluginId => AntiFloodPlugin.Id;

    public int Order => 40;

    public Localized Title { get; } = new("🌊 ضدفلود", "🌊 Anti-flood");

    public bool HasPluginSwitch => false;

    public async Task<PanelView> ShowAsync(PanelContext context, CancellationToken ct)
    {
        var s = await states.GetSettingsAsync<AntiFloodSettings>(context.ChatId, AntiFloodPlugin.Id, ct);
        var lang = context.Lang;
        var muteFor = TimeSpan.FromMinutes(s.MuteMinutes);

        var rows = new List<InlineKeyboardButton[]>
        {
            new[] { FloodLevel.Off, FloodLevel.Low }.Select(l => LevelButton(context, s, l)).ToArray(),
            new[] { FloodLevel.Medium, FloodLevel.Strict }.Select(l => LevelButton(context, s, l)).ToArray(),
            MuteChoices.Select(m => PanelButtons.Action(PanelButtons.Mark(s.MuteMinutes == m, "🔇 " + Duration.Format(TimeSpan.FromMinutes(m), lang)),
                context, Id, "mute", m.ToString())).ToArray(),
            new[] { PanelButtons.BackToGroup(context) },
        };

        var html = context.T(Text).Replace("{0}", Describe(s.Level, lang)).Replace("{1}", Duration.Format(muteFor, lang));
        return new PanelView(html, new InlineKeyboardMarkup(rows));
    }

    public async Task<string?> HandleAsync(PanelContext context, string[] args, CancellationToken ct)
    {
        var s = await states.GetSettingsAsync<AntiFloodSettings>(context.ChatId, AntiFloodPlugin.Id, ct);
        var updated = new AntiFloodSettings { Level = s.Level, MuteMinutes = s.MuteMinutes };
        var value = args.Length > 1 ? args[1] : "";

        if (args[0] == "lvl" && Enum.TryParse<FloodLevel>(value, out var level) && Enum.IsDefined(level)) updated.Level = level;
        else if (args[0] == "mute" && int.TryParse(value, out var minutes) && MuteChoices.Contains(minutes)) updated.MuteMinutes = minutes;
        else return null;

        await states.SaveSettingsAsync(context.ChatId, AntiFloodPlugin.Id, updated, ct);
        return null;
    }

    private InlineKeyboardButton LevelButton(PanelContext context, AntiFloodSettings s, FloodLevel level) =>
        PanelButtons.Action(PanelButtons.Mark(s.Level == level, Describe(level, context.Lang)), context, Id, "lvl", level.ToString());

    private static string Describe(FloodLevel level, string lang)
    {
        var fa = lang != Languages.English;
        return level switch
        {
            FloodLevel.Off => fa ? "خاموش" : "Off",
            FloodLevel.Low => fa ? $"کم ({AntiFloodSettings.LimitFor(level)} پیام)" : $"Low ({AntiFloodSettings.LimitFor(level)} msgs)",
            FloodLevel.Medium => fa ? $"متوسط ({AntiFloodSettings.LimitFor(level)} پیام)" : $"Medium ({AntiFloodSettings.LimitFor(level)} msgs)",
            _ => fa ? $"سخت ({AntiFloodSettings.LimitFor(level)} پیام)" : $"Strict ({AntiFloodSettings.LimitFor(level)} msgs)",
        };
    }
}
