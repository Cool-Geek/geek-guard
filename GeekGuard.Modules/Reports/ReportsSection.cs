using GeekGuard.Core.Messaging;
using GeekGuard.Core.Panel;
using Telegram.Bot.Types.ReplyMarkups;

namespace GeekGuard.Modules.Reports;

/// <summary>Panel: member reports on/off.</summary>
public sealed class ReportsSection : IPanelSection
{
    private static readonly Localized Text = new(
        "🚩 <b>گزارش به ادمین</b>\n\nاعضا با ریپلای «گزارش» روی یک پیام، آن را به ادمین‌ها گزارش می‌دهند. گزارش در پیوی ربات به ادمین‌هایی می‌رسد که ربات را استارت کرده‌اند، با دکمه‌های حذف، سکوت و بن.",
        "🚩 <b>Reports</b>\n\nMembers reply /report to a message to flag it. Admins who started the bot get it privately, with delete, mute and ban buttons.");

    public string Id => "reports";

    public string PluginId => ReportsPlugin.Id;

    public int Order => 60;

    public Localized Title { get; } = new("🚩 گزارش‌ها", "🚩 Reports");

    public bool HasPluginSwitch => true;

    public Task<PanelView> ShowAsync(PanelContext context, CancellationToken ct) =>
        Task.FromResult(new PanelView(context.T(Text), new InlineKeyboardMarkup(new[]
        {
            new[] { PanelButtons.PluginSwitch(context, Id) },
            new[] { PanelButtons.BackToGroup(context) },
        })));

    public Task<string?> HandleAsync(PanelContext context, string[] args, CancellationToken ct) => Task.FromResult<string?>(null);
}
