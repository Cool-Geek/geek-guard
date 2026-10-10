using System.Collections.Concurrent;
using GeekGuard.Core.Callbacks;
using GeekGuard.Core.Groups;
using GeekGuard.Core.Messaging;
using GeekGuard.Core.Panel;
using GeekGuard.Core.Plugins;
using GeekGuard.Core.Users;
using Telegram.Bot.Types;
using Telegram.Bot.Types.ReplyMarkups;

namespace GeekGuard.Bot.Panel;

/// <summary>
/// The settings panel in the bot's private chat: the admin's groups, each group's menu, and the sections plugins add.
/// Every press edits the same message, so the chat stays one tidy panel instead of a pile of messages.
/// </summary>
/// <remarks>
/// Button data: "p:h" home, "p:l" switch the panel's language, "p:g:&lt;chat&gt;" a group's menu,
/// "p:s:&lt;chat&gt;:&lt;section&gt;[:args]" a section or an action in it, "p:t:&lt;chat&gt;:&lt;section&gt;" the section's on/off switch,
/// "p:i:&lt;chat&gt;:&lt;section&gt;:&lt;field&gt;" ask the admin to type text for a field.
/// </remarks>
public sealed class PanelNavigator(
    IEnumerable<IPanelSection> sections,
    PluginCatalog catalog,
    PluginStateStore states,
    GroupDirectory groups,
    GroupAdmins admins,
    UserRepository users,
    BotActions actions,
    BotIdentity me) : ICallbackHandler
{
    /// <summary>How long the panel waits for typed text before forgetting the question.</summary>
    private static readonly TimeSpan InputTimeout = TimeSpan.FromMinutes(10);

    /// <summary>Admins the panel is waiting on for typed text. In memory: after a restart they just tap again.</summary>
    private readonly ConcurrentDictionary<long, PendingInput> _pending = new();

    private sealed record PendingInput(long ChatId, string SectionId, string Field, int PanelMessageId, DateTime Expires);

    private readonly IPanelSection[] _sections = sections.Where(s => catalog.Contains(s.PluginId)).OrderBy(s => s.Order).ToArray();

    private static readonly Localized Hello = new(
        "سلام {0}! 👋\nمن <b>Geek Guard</b> هستم، مدیر گروه‌های تلگرام.\n\n{1}",
        "Hi {0}! 👋\nI'm <b>Geek Guard</b>, a Telegram group manager.\n\n{1}");
    private static readonly Localized PickGroup = new("⚙️ گروهی را که می‌خواهی تنظیم کنی انتخاب کن:", "⚙️ Pick a group to set up:");
    private static readonly Localized NoGroups = new(
        "هنوز گروهی نیست که هم من آنجا باشم و هم تو ادمینش باشی.\nمرا به گروهت اضافه کن و ادمین کن (با اجازه‌ی حذف پیام و محدود کردن اعضا)، بعد دوباره /start بزن.",
        "There is no group yet where I'm in and you're an admin.\nAdd me to your group as an admin (delete messages + restrict members), then send /start again.");
    private static readonly Localized AddToGroup = new("➕ افزودن من به گروه", "➕ Add me to a group");
    private static readonly Localized SwitchLang = new("🌐 English", "🌐 فارسی");
    private static readonly Localized Guide = new("📖 راهنما", "📖 Guide");
    private static readonly Localized GroupTitle = new("⚙️ تنظیمات «{0}»", "⚙️ Settings of “{0}”");
    private static readonly Localized ViewOnly = new(
        "👁 فقط مشاهده: تغییر تنظیمات برای سازنده‌ی گروه و ادمین‌هایی است که اجازه‌ی «محدود کردن اعضا» دارند.",
        "👁 View only: settings can be changed by the owner and admins who may restrict members.");
    private static readonly Localized ChooseSection = new("یک بخش را انتخاب کن:", "Choose a section:");
    private static readonly Localized BackHome = new("🔙 گروه‌ها", "🔙 Groups");
    private static readonly Localized NotAdmin = new("⛔ ادمین این گروه نیستی.", "⛔ You are not an admin of that group.");
    private static readonly Localized CannotEdit = new(
        "⛔ فقط سازنده و ادمین‌هایی که اجازه‌ی «محدود کردن اعضا» دارند می‌توانند تغییر بدهند.",
        "⛔ Only the owner and admins who may restrict members can change this.");
    private static readonly Localized Saved = new("✅ ذخیره شد", "✅ Saved");
    private static readonly Localized Cancel = new("❌ لغو", "❌ Cancel");
    private static readonly Localized TypeHint = new(
        "✏️ {0}\n\n<i>پیامت را همین‌جا بفرست، یا «لغو» را بزن.</i>",
        "✏️ {0}\n\n<i>Send it here as a message, or tap Cancel.</i>");

    public string Prefix => PanelButtons.Prefix;

    /// <summary>Sends the panel's home screen as a new message (for /start in the private chat).</summary>
    public async Task ShowHomeAsync(long chatId, User user, CancellationToken ct)
    {
        _pending.TryRemove(user.Id, out _);
        var (html, keyboard) = await HomeAsync(user, ct);
        await actions.SendAsync(chatId, html, keyboard: keyboard, ct: ct);
    }

    public async Task HandleAsync(CallbackQuery query, string[] args, CancellationToken ct)
    {
        // Panel buttons only live in the private chat; anything else is stale or forged.
        if (query.Message is not { Chat.Type: Telegram.Bot.Types.Enums.ChatType.Private } panel || args.Length == 0)
        {
            await actions.AnswerButtonAsync(query.Id, ct: ct);
            return;
        }

        var user = query.From;
        var lang = await UserLangAsync(user, ct);

        // Any other press means the admin moved on: stop waiting for typed text.
        if (args[0] != "i") _pending.TryRemove(user.Id, out _);
        string? note = null;
        PanelView? view;

        switch (args[0])
        {
            case "n":
                // A title row: nothing to do.
                await actions.AnswerButtonAsync(query.Id, ct: ct);
                return;

            case "l":
                lang = lang == Languages.English ? Languages.Persian : Languages.English;
                await users.SetLangAsync(user.Id, lang, ct);
                view = await HomeAsync(user, ct);
                break;

            case "g" when args.Length == 2 && long.TryParse(args[1], out var menuChat):
                var menuContext = await ContextAsync(menuChat, user.Id, lang, null, ct);
                if (menuContext is null) { await RefuseAsync(query, NotAdmin, lang, user, panel, ct); return; }
                view = GroupMenu(menuContext);
                break;

            case "i" when args.Length == 4 && long.TryParse(args[1], out var inputChat)
                          && _sections.FirstOrDefault(s => s.Id == args[2]) is { } inputSection
                          && inputSection is IPanelTextInput input:
                var inputContext = await ContextAsync(inputChat, user.Id, lang, inputSection, ct);
                if (inputContext is null) { await RefuseAsync(query, NotAdmin, lang, user, panel, ct); return; }
                if (!inputContext.CanEdit)
                {
                    await actions.AnswerButtonAsync(query.Id, CannotEdit.Get(lang), alert: true, ct);
                    return;
                }

                _pending[user.Id] = new PendingInput(inputChat, inputSection.Id, args[3], panel.MessageId, DateTime.UtcNow + InputTimeout);
                view = new PanelView(TypeHint.Format(lang, input.Prompt(args[3]).Get(lang)), new InlineKeyboardMarkup(
                    PanelButtons.Open(Cancel.Get(lang), inputChat, inputSection.Id)));
                break;

            case "s" or "t" when args.Length >= 3 && long.TryParse(args[1], out var chatId)
                                 && _sections.FirstOrDefault(s => s.Id == args[2]) is { } section:
                var context = await ContextAsync(chatId, user.Id, lang, section, ct);
                if (context is null) { await RefuseAsync(query, NotAdmin, lang, user, panel, ct); return; }

                var changes = args[0] == "t" || args.Length > 3;
                if (changes && !context.CanEdit)
                {
                    await actions.AnswerButtonAsync(query.Id, CannotEdit.Get(lang), alert: true, ct);
                    return;
                }

                if (args[0] == "t")
                {
                    await states.SetEnabledAsync(chatId, section.PluginId, !context.PluginEnabled, ct);
                    note = Saved.Get(lang);
                }
                else if (args.Length > 3)
                {
                    note = await section.HandleAsync(context, args[3..], ct) ?? Saved.Get(lang);
                }

                // Show the section again, with the plugin switch as it is now.
                if (changes) context = await ContextAsync(chatId, user.Id, lang, section, ct) ?? context;
                view = await section.ShowAsync(context, ct);
                break;

            default:
                view = await HomeAsync(user, ct);
                break;
        }

        await actions.AnswerButtonAsync(query.Id, note, ct: ct);
        await actions.EditAsync(panel.Chat.Id, panel.MessageId, view.Html, view.Keyboard, ct);
    }

    /// <summary>
    /// A private message from an admin the panel asked for text. Returns false when nobody asked, so the message
    /// is treated as ordinary chat.
    /// </summary>
    public async Task<bool> TryAcceptTextAsync(Message message, CancellationToken ct)
    {
        if (message is not { From: { } user, Text: { } text } || !_pending.TryGetValue(user.Id, out var pending)) return false;
        if (pending.Expires < DateTime.UtcNow)
        {
            _pending.TryRemove(user.Id, out _);
            return false;
        }

        var lang = await UserLangAsync(user, ct);
        var section = _sections.First(s => s.Id == pending.SectionId);
        var context = await ContextAsync(pending.ChatId, user.Id, lang, section, ct);
        if (context is not { CanEdit: true })
        {
            _pending.TryRemove(user.Id, out _);
            await actions.SendAsync(message.Chat.Id, (context is null ? NotAdmin : CannotEdit).Get(lang), ct: ct);
            return true;
        }

        var result = await ((IPanelTextInput)section).AcceptTextAsync(context, pending.Field, text.Trim(), ct);
        if (!result.Accepted)
        {
            // Still waiting: the admin can send a better text or cancel.
            await actions.SendAsync(message.Chat.Id, result.Note ?? "⚠️", ct: ct);
            return true;
        }

        _pending.TryRemove(user.Id, out _);

        // The old panel is now above the admin's message; replace it with a fresh one below.
        await actions.DeleteAsync(message.Chat.Id, pending.PanelMessageId, ct);
        var view = await section.ShowAsync(context, ct);
        var html = string.IsNullOrEmpty(result.Note) ? view.Html : $"{result.Note}\n\n{view.Html}";
        await actions.SendAsync(message.Chat.Id, html, keyboard: view.Keyboard, ct: ct);
        return true;
    }

    private async Task<PanelView> HomeAsync(User user, CancellationToken ct)
    {
        var lang = await UserLangAsync(user, ct);
        // The stored list says where the user was an admin; Telegram is asked again (cached for minutes) so a group
        // they no longer administer, or one they were only ever a member of, never shows up.
        var mine = new List<GroupInfo>();
        foreach (var group in await groups.ListForAdminAsync(user.Id, ct))
        {
            if (await admins.IsAdminAsync(group.ChatId, user.Id, ct)) mine.Add(group);
        }

        var rows = mine.Select(g => new[]
        {
            InlineKeyboardButton.WithCallbackData("👥 " + Html.Truncate(g.Title.Length > 0 ? g.Title : g.ChatId.ToString(), 40),
                PanelButtons.Data("g", g.ChatId.ToString())),
        }).ToList();
        rows.Add([InlineKeyboardButton.WithUrl(AddToGroup.Get(lang), $"https://t.me/{me.Username}?startgroup=true")]);
        rows.Add([
            InlineKeyboardButton.WithCallbackData(Guide.Get(lang), "hp:h"),
            InlineKeyboardButton.WithCallbackData(SwitchLang.Get(lang), PanelButtons.Data("l")),
        ]);

        var html = Hello.Format(lang, Html.Escape(user.FirstName), (mine.Count > 0 ? PickGroup : NoGroups).Get(lang));
        return new PanelView(html, new InlineKeyboardMarkup(rows));
    }

    private PanelView GroupMenu(PanelContext context)
    {
        var lang = context.Lang;
        var html = $"<b>{GroupTitle.Format(lang, Html.Escape(context.Group.Title))}</b>\n\n"
                   + (context.CanEdit ? "" : ViewOnly.Get(lang) + "\n\n")
                   + ChooseSection.Get(lang);

        var rows = _sections
            .Select(s => PanelButtons.Open(s.Title.Get(lang), context.ChatId, s.Id))
            .Chunk(2)
            .ToList();
        rows.Add([InlineKeyboardButton.WithCallbackData(BackHome.Get(lang), PanelButtons.Data("h"))]);
        return new PanelView(html, new InlineKeyboardMarkup(rows));
    }

    /// <summary>
    /// Checks, on every press, that the user is still an admin of the group (rights change; buttons stay).
    /// Null when they are not.
    /// </summary>
    private async Task<PanelContext?> ContextAsync(long chatId, long userId, string lang, IPanelSection? section, CancellationToken ct)
    {
        if (await groups.GetAsync(chatId, ct) is not { Active: true } group) return null;
        if (!await admins.IsAdminAsync(chatId, userId, ct)) return null;

        var enabled = section is not null && await states.IsActiveAsync(chatId, catalog.Get(section.PluginId), ct);
        return new PanelContext
        {
            Group = group,
            UserId = userId,
            Lang = lang,
            CanEdit = await admins.CanRestrictAsync(chatId, userId, ct),
            PluginEnabled = enabled,
        };
    }

    /// <summary>Says no, and puts the admin back on the home screen with an up-to-date group list.</summary>
    private async Task RefuseAsync(CallbackQuery query, Localized reason, string lang, User user, Message panel, CancellationToken ct)
    {
        await actions.AnswerButtonAsync(query.Id, reason.Get(lang), alert: true, ct);
        var home = await HomeAsync(user, ct);
        await actions.EditAsync(panel.Chat.Id, panel.MessageId, home.Html, home.Keyboard, ct);
    }

    /// <summary>The language the user chose, or the one their Telegram app uses.</summary>
    private async Task<string> UserLangAsync(User user, CancellationToken ct) =>
        (await users.GetAsync(user.Id, ct))?.Lang ?? Languages.FromTelegram(user.LanguageCode);
}
