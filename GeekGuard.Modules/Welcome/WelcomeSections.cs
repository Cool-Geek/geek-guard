using GeekGuard.Core.Messaging;
using GeekGuard.Core.Panel;
using Telegram.Bot.Types;
using Telegram.Bot.Types.ReplyMarkups;

namespace GeekGuard.Modules.Welcome;

/// <summary>Panel: welcome on/off, its text, and whether only the latest welcome stays.</summary>
public sealed class WelcomeSection(PluginStateStore states) : IPanelSection, IPanelTextInput
{
    private static readonly Localized Text = new(
        "👋 <b>خوشامدگویی</b>\n\nوضعیت: <b>{0}</b>\nپاک شدن خوشامد قبلی با ورود نفر بعد: <b>{1}</b>\n\nپیش‌نمایش:\n{2}",
        "👋 <b>Welcome</b>\n\nStatus: <b>{0}</b>\nRemove the previous welcome when someone new joins: <b>{1}</b>\n\nPreview:\n{2}");
    private static readonly Localized Ask = new(
        "متن خوشامد جدید را بفرست. می‌توانی از {name} (اسم عضو جدید)، {group} (اسم گروه) و {count} (تعداد اعضا) استفاده کنی.",
        "Send the new welcome text. You can use {name} (the newcomer), {group} (the group's name) and {count} (number of members).");
    private static readonly Localized TooLong = new("⚠️ متن خوشامد حداکثر {0} حرف می‌تواند باشد. کوتاه‌ترش کن و دوباره بفرست.",
        "⚠️ The welcome can be at most {0} characters. Shorten it and send again.");
    private static readonly Localized SavedNote = new("✅ متن خوشامد ثبت شد.", "✅ Welcome text saved.");

    public string Id => "welcome";

    public string PluginId => WelcomePlugin.Id;

    public int Order => 70;

    public Localized Title { get; } = new("👋 خوشامدگویی", "👋 Welcome");

    public bool HasPluginSwitch => false;

    public async Task<PanelView> ShowAsync(PanelContext context, CancellationToken ct)
    {
        var s = await states.GetSettingsAsync<WelcomeSettings>(context.ChatId, WelcomePlugin.Id, ct);
        var fa = context.Lang != Languages.English;

        // The preview is in the group's language: that is what newcomers will read.
        var sample = new[] { new User { Id = context.UserId, FirstName = context.Group.Lang == Languages.English ? "New member" : "عضو جدید" } };
        var preview = s.Render(context.Group.Lang, sample, context.Group.Title, 123);

        var html = context.T(Text)
            .Replace("{0}", s.Enabled ? (fa ? "روشن ✅" : "on ✅") : (fa ? "خاموش ❌" : "off ❌"))
            .Replace("{1}", s.DeletePrevious ? "✅" : "❌")
            .Replace("{2}", preview);

        var keyboard = new InlineKeyboardMarkup(new[]
        {
            new[] { PanelButtons.Action(s.Enabled ? (fa ? "✅ روشن — زدن برای خاموش کردن" : "✅ On — tap to turn off")
                                                  : (fa ? "❌ خاموش — زدن برای روشن کردن" : "❌ Off — tap to turn on"), context, Id, "on") },
            new[]
            {
                PanelButtons.AskText(fa ? "✏️ ویرایش متن" : "✏️ Edit text", context, Id, "text"),
                PanelButtons.Action(fa ? "↩️ متن پیش‌فرض" : "↩️ Default text", context, Id, "reset"),
            },
            new[] { PanelButtons.Action((s.DeletePrevious ? "✅ " : "❌ ") + (fa ? "فقط آخرین خوشامد بماند" : "Keep only the latest welcome"), context, Id, "prev") },
            new[] { PanelButtons.BackToGroup(context) },
        });
        return new PanelView(html, keyboard);
    }

    public async Task<string?> HandleAsync(PanelContext context, string[] args, CancellationToken ct)
    {
        var s = await states.GetSettingsAsync<WelcomeSettings>(context.ChatId, WelcomePlugin.Id, ct);
        var updated = new WelcomeSettings { Enabled = s.Enabled, Text = s.Text, DeletePrevious = s.DeletePrevious };
        switch (args[0])
        {
            case "on": updated.Enabled = !s.Enabled; break;
            case "reset": updated.Text = ""; break;
            case "prev": updated.DeletePrevious = !s.DeletePrevious; break;
            default: return null;
        }
        await states.SaveSettingsAsync(context.ChatId, WelcomePlugin.Id, updated, ct);
        return null;
    }

    public Localized Prompt(string field) => Ask;

    public async Task<PanelInputResult> AcceptTextAsync(PanelContext context, string field, string text, CancellationToken ct)
    {
        if (text.Length == 0) return new PanelInputResult(false, context.T(Ask));
        if (text.Length > WelcomeSettings.MaxLength) return new PanelInputResult(false, TooLong.Format(context.Lang, WelcomeSettings.MaxLength));

        var s = await states.GetSettingsAsync<WelcomeSettings>(context.ChatId, WelcomePlugin.Id, ct);
        // Writing a welcome also switches it on: that is clearly what the admin wants.
        await states.SaveSettingsAsync(context.ChatId, WelcomePlugin.Id,
            new WelcomeSettings { Enabled = true, Text = text, DeletePrevious = s.DeletePrevious }, ct);
        return new PanelInputResult(true, context.T(SavedNote));
    }
}

/// <summary>Panel: the group's rules.</summary>
public sealed class RulesSection(PluginStateStore states) : IPanelSection, IPanelTextInput
{
    private const int PreviewLength = 1500;

    private static readonly Localized Text = new(
        "📜 <b>قوانین گروه</b>\n\nاعضا با «قوانین» یا دکمه‌ی زیر پیام خوشامد، قوانین را در پیوی ربات می‌خوانند.\n\n{0}",
        "📜 <b>Group rules</b>\n\nMembers read them in a private chat with the bot, via /rules or the button under the welcome.\n\n{0}");
    private static readonly Localized None = new("<i>هنوز قانونی ثبت نشده.</i>", "<i>No rules yet.</i>");
    private static readonly Localized Ask = new("متن کامل قوانین را بفرست (خط‌بندی حفظ می‌شود).", "Send the full rules (line breaks are kept).");
    private static readonly Localized TooLong = new("⚠️ قوانین حداکثر {0} حرف می‌تواند باشد. کوتاه‌ترش کن و دوباره بفرست.",
        "⚠️ Rules can be at most {0} characters. Shorten them and send again.");
    private static readonly Localized SavedNote = new("✅ قوانین ثبت شد.", "✅ Rules saved.");

    public string Id => "rules";

    public string PluginId => RulesPlugin.Id;

    public int Order => 80;

    public Localized Title { get; } = new("📜 قوانین", "📜 Rules");

    public bool HasPluginSwitch => false;

    public async Task<PanelView> ShowAsync(PanelContext context, CancellationToken ct)
    {
        var s = await states.GetSettingsAsync<RulesSettings>(context.ChatId, RulesPlugin.Id, ct);
        var fa = context.Lang != Languages.English;
        var body = s.Text.Length == 0 ? context.T(None) : $"<blockquote>{Html.Escape(Html.Truncate(s.Text, PreviewLength))}</blockquote>";

        var rows = new List<InlineKeyboardButton[]>
        {
            new[] { PanelButtons.AskText(fa ? "✏️ نوشتن قوانین" : "✏️ Write rules", context, Id, "text") },
        };
        if (s.Text.Length > 0) rows.Add(new[] { PanelButtons.Action(fa ? "🗑 حذف قوانین" : "🗑 Remove rules", context, Id, "clear") });
        rows.Add(new[] { PanelButtons.BackToGroup(context) });
        return new PanelView(context.T(Text).Replace("{0}", body), new InlineKeyboardMarkup(rows));
    }

    public async Task<string?> HandleAsync(PanelContext context, string[] args, CancellationToken ct)
    {
        if (args[0] == "clear") await states.SaveSettingsAsync(context.ChatId, RulesPlugin.Id, new RulesSettings(), ct);
        return null;
    }

    public Localized Prompt(string field) => Ask;

    public async Task<PanelInputResult> AcceptTextAsync(PanelContext context, string field, string text, CancellationToken ct)
    {
        if (text.Length == 0) return new PanelInputResult(false, context.T(Ask));
        if (text.Length > RulesSettings.MaxLength) return new PanelInputResult(false, TooLong.Format(context.Lang, RulesSettings.MaxLength));

        await states.SaveSettingsAsync(context.ChatId, RulesPlugin.Id, new RulesSettings { Text = text }, ct);
        return new PanelInputResult(true, context.T(SavedNote));
    }
}
