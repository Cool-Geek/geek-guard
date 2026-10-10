using GeekGuard.Core.Groups;
using GeekGuard.Core.Messaging;
using GeekGuard.Core.Panel;
using Telegram.Bot.Types.ReplyMarkups;

namespace GeekGuard.Modules.GroupSettings;

/// <summary>Panel: the language of the bot's messages in the group.</summary>
public sealed class LanguageSection(GroupDirectory groups) : IPanelSection
{
    private static readonly Localized Text = new(
        "🌐 <b>زبان گروه</b>\n\nپیام‌های ربات در این گروه به این زبان نوشته می‌شوند. (زبان همین پنل جداست و از صفحه‌ی اول عوض می‌شود.)",
        "🌐 <b>Group language</b>\n\nThe bot writes its messages in this group in this language. (This panel's own language is separate; change it on the first screen.)");

    public string Id => "lang";

    public string PluginId => GroupSettingsPlugin.Id;

    public int Order => 10;

    public Localized Title { get; } = new("🌐 زبان گروه", "🌐 Language");

    public bool HasPluginSwitch => false;

    public Task<PanelView> ShowAsync(PanelContext context, CancellationToken ct)
    {
        var current = context.Group.Lang;
        var keyboard = new InlineKeyboardMarkup(new[]
        {
            new[]
            {
                PanelButtons.Action(PanelButtons.Mark(current == Languages.Persian, "فارسی"), context, Id, Languages.Persian),
                PanelButtons.Action(PanelButtons.Mark(current == Languages.English, "English"), context, Id, Languages.English),
            },
            new[] { PanelButtons.BackToGroup(context) },
        });
        return Task.FromResult(new PanelView(context.T(Text), keyboard));
    }

    public async Task<string?> HandleAsync(PanelContext context, string[] args, CancellationToken ct)
    {
        if (args[0] is Languages.Persian or Languages.English) await groups.SetLangAsync(context.ChatId, args[0], ct);
        return null;
    }
}
