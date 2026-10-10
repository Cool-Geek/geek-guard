using GeekGuard.Core.Messaging;
using GeekGuard.Core.Panel;
using Telegram.Bot.Types.ReplyMarkups;

namespace GeekGuard.Modules.AntiLink;

/// <summary>Panel: anti-link on/off and whether offenders are warned.</summary>
public sealed class AntiLinkSection(PluginStateStore states) : IPanelSection
{
    private static readonly Localized Text = new(
        "🔗 <b>ضدلینک</b>\n\nلینک‌هایی که اعضا می‌فرستند پاک می‌شود، حتی لینک‌های پنهان پشت متن، لینک‌های دکمه‌ای و شکل‌هایی مثل «t . me». پیام ادمین‌ها دست نمی‌خورد.\n\nاخطار به فرستنده: <b>{0}</b>",
        "🔗 <b>Anti-link</b>\n\nLinks from members are removed, including links hidden behind text, link buttons and tricks like “t . me”. Admins are never touched.\n\nWarn the sender: <b>{0}</b>");

    public string Id => "link";

    public string PluginId => AntiLinkPlugin.Id;

    public int Order => 30;

    public Localized Title { get; } = new("🔗 ضدلینک", "🔗 Anti-link");

    public bool HasPluginSwitch => true;

    public async Task<PanelView> ShowAsync(PanelContext context, CancellationToken ct)
    {
        var s = await states.GetSettingsAsync<AntiLinkSettings>(context.ChatId, AntiLinkPlugin.Id, ct);
        var fa = context.Lang != Languages.English;
        var keyboard = new InlineKeyboardMarkup(new[]
        {
            PanelButtons.Header(fa ? "🔗 ضدلینک" : "🔗 Anti-link"),
            new[] { PanelButtons.PluginSwitch(context, Id) },
            PanelButtons.Header(fa ? "⚠️ فرستنده‌ی لینک" : "⚠️ Sender of a link"),
            new[] { PanelButtons.Toggle(fa ? "اخطار بگیرد" : "Gets a warning", s.WarnOnViolation, context, Id, "warn") },
            new[] { PanelButtons.BackToGroup(context) },
        });
        return new PanelView(context.T(Text).Replace("{0}", s.WarnOnViolation ? "✅" : "❌"), keyboard);
    }

    public async Task<string?> HandleAsync(PanelContext context, string[] args, CancellationToken ct)
    {
        if (args[0] != "warn") return null;
        var s = await states.GetSettingsAsync<AntiLinkSettings>(context.ChatId, AntiLinkPlugin.Id, ct);
        await states.SaveSettingsAsync(context.ChatId, AntiLinkPlugin.Id, new AntiLinkSettings { WarnOnViolation = !s.WarnOnViolation }, ct);
        return null;
    }
}
