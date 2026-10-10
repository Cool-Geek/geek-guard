using GeekGuard.Core.Callbacks;
using GeekGuard.Core.Help;
using GeekGuard.Core.Messaging;
using GeekGuard.Core.Plugins;
using GeekGuard.Core.Private;
using GeekGuard.Core.Users;
using Telegram.Bot.Types;
using Telegram.Bot.Types.ReplyMarkups;

namespace GeekGuard.Bot.Help;

/// <summary>
/// The user guide in the private chat: a menu of pages contributed by the plugins, each marked 🆓 or 💎.
/// Opened with /help, from the panel's 📖 button, or from the group with /help (a link to here).
/// Button data: "hp:h" the menu, "hp:&lt;topic id&gt;" a page.
/// </summary>
public sealed class HelpNavigator(
    IEnumerable<HelpTopic> topics,
    PluginCatalog catalog,
    UserRepository users,
    BotActions actions) : ICallbackHandler, IStartLinkHandler
{
    public const string ButtonPrefix = "hp";

    /// <summary>Start-link prefix used by the group's /help: t.me/bot?start=help_0.</summary>
    public const string LinkPrefix = "help";

    private readonly HelpTopic[] _topics = topics.Where(t => catalog.Contains(t.PluginId)).OrderBy(t => t.Order).ToArray();

    private static readonly Localized Intro = new(
        "📖 <b>راهنمای Geek Guard</b>\n\nیک بخش را انتخاب کن. 🆓 رایگان — 💎 نسخه‌ی Pro\n\nبرای تنظیم گروه‌هایت /start بزن.",
        "📖 <b>Geek Guard guide</b>\n\nPick a topic. 🆓 free — 💎 Pro\n\nSend /start to set up your groups.");
    private static readonly Localized BackToGuide = new("🔙 راهنما", "🔙 Guide");
    private static readonly Localized BackToPanel = new("⚙️ پنل تنظیمات", "⚙️ Settings panel");

    string ICallbackHandler.Prefix => ButtonPrefix;

    string IStartLinkHandler.Prefix => LinkPrefix;

    /// <summary>Sends the guide's menu as a new message.</summary>
    public async Task ShowHomeAsync(long chatId, User user, CancellationToken ct)
    {
        var view = Menu(await LangAsync(user, ct));
        await actions.SendAsync(chatId, view.Html, keyboard: view.Keyboard, ct: ct);
    }

    public Task HandleAsync(Message message, string argument, CancellationToken ct) =>
        message.From is { } user ? ShowHomeAsync(message.Chat.Id, user, ct) : Task.CompletedTask;

    public async Task HandleAsync(CallbackQuery query, string[] args, CancellationToken ct)
    {
        await actions.AnswerButtonAsync(query.Id, ct: ct);
        if (query.Message is not { } message) return;

        var lang = await LangAsync(query.From, ct);
        var topic = args.Length == 1 ? _topics.FirstOrDefault(t => t.Id == args[0]) : null;
        var (html, keyboard) = topic is null ? Menu(lang) : Page(topic, lang);
        await actions.EditAsync(message.Chat.Id, message.MessageId, html, keyboard, ct);
    }

    private (string Html, InlineKeyboardMarkup Keyboard) Menu(string lang)
    {
        var rows = _topics
            .Select(t => InlineKeyboardButton.WithCallbackData($"{Badge(t)} {t.Title.Get(lang)}", $"{ButtonPrefix}:{t.Id}"))
            .Chunk(2)
            .ToList();
        rows.Add([InlineKeyboardButton.WithCallbackData(BackToPanel.Get(lang), "p:h")]);
        return (Intro.Get(lang), new InlineKeyboardMarkup(rows));
    }

    private (string Html, InlineKeyboardMarkup Keyboard) Page(HelpTopic topic, string lang)
    {
        var tier = catalog.Get(topic.PluginId).Tier == PluginTier.Pro
            ? (lang == Languages.English ? "💎 Pro" : "💎 نسخه‌ی Pro")
            : (lang == Languages.English ? "🆓 Free" : "🆓 رایگان");
        var html = $"{topic.Body.Get(lang).Trim()}\n\n<i>{tier}</i>";
        var keyboard = new InlineKeyboardMarkup(new[]
        {
            new[] { InlineKeyboardButton.WithCallbackData(BackToGuide.Get(lang), $"{ButtonPrefix}:h") },
        });
        return (html, keyboard);
    }

    private string Badge(HelpTopic topic) => catalog.Get(topic.PluginId).Tier == PluginTier.Pro ? "💎" : "🆓";

    private async Task<string> LangAsync(User user, CancellationToken ct) =>
        (await users.GetAsync(user.Id, ct))?.Lang ?? Languages.FromTelegram(user.LanguageCode);
}
