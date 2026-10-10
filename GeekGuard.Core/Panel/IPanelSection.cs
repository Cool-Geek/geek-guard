using GeekGuard.Core.Groups;
using GeekGuard.Core.Messaging;
using Telegram.Bot.Types.ReplyMarkups;

namespace GeekGuard.Core.Panel;

/// <summary>A screen of the settings panel: its text and its buttons.</summary>
public sealed record PanelView(string Html, InlineKeyboardMarkup Keyboard);

/// <summary>Who is looking at which group's panel, and what they may do there.</summary>
public sealed class PanelContext
{
    public required GroupInfo Group { get; init; }

    public long ChatId => Group.ChatId;

    /// <summary>The admin using the panel.</summary>
    public required long UserId { get; init; }

    /// <summary>The panel speaks the admin's language, not the group's.</summary>
    public required string Lang { get; init; }

    /// <summary>
    /// The owner and admins who may restrict members can change settings; other admins only look.
    /// Sections never need to check this: presses that change something are refused before they arrive.
    /// </summary>
    public required bool CanEdit { get; init; }

    /// <summary>Whether the section's plugin is switched on in this group.</summary>
    public required bool PluginEnabled { get; init; }

    public string T(Localized text) => text.Get(Lang);
}

/// <summary>
/// One section of a group's settings panel, contributed by a plugin (Pro plugins add their own the same way).
/// Register with <c>services.AddPanelSection&lt;T&gt;()</c>.
/// </summary>
public interface IPanelSection
{
    /// <summary>Short id used in button data, e.g. "flood". Letters only, unique.</summary>
    string Id { get; }

    /// <summary>The plugin whose settings this section edits.</summary>
    string PluginId { get; }

    /// <summary>Position in the group menu: lower comes first.</summary>
    int Order { get; }

    /// <summary>Button label in the group menu, e.g. «🌊 ضدفلود».</summary>
    Localized Title { get; }

    /// <summary>Whether the section offers an on/off switch for its whole plugin.</summary>
    bool HasPluginSwitch { get; }

    Task<PanelView> ShowAsync(PanelContext context, CancellationToken ct);

    /// <summary>
    /// A button of this section was pressed (only ever by someone allowed to edit). <paramref name="args"/> are what
    /// the section put after its id in <see cref="PanelButtons.Action"/>. Returns a short note for the admin, or null.
    /// The section is shown again afterwards.
    /// </summary>
    Task<string?> HandleAsync(PanelContext context, string[] args, CancellationToken ct);
}

/// <summary>
/// A section that also takes typed text (a welcome message, rules, words to filter). A button made with
/// <see cref="PanelButtons.AskText"/> asks the admin to send the text; their next private message comes here.
/// </summary>
public interface IPanelTextInput
{
    /// <summary>What the admin is asked, e.g. «متن خوشامد جدید را بفرست».</summary>
    Localized Prompt(string field);

    /// <summary>
    /// The admin sent the text (they are allowed to edit; that was checked again). Return Accepted = false with a note
    /// to ask again (too long, empty…); the admin can also cancel.
    /// </summary>
    Task<PanelInputResult> AcceptTextAsync(PanelContext context, string field, string text, CancellationToken ct);
}

/// <param name="Accepted">The text was used; the section is shown again.</param>
/// <param name="Note">Shown above the section, or as the reason to try again.</param>
public sealed record PanelInputResult(bool Accepted, string? Note);

/// <summary>
/// Button data for the panel. Everything starts with "p:"; group screens carry the chat id, so a button always says
/// which group it belongs to and the rights are checked again on every press.
/// </summary>
public static class PanelButtons
{
    public const string Prefix = "p";

    /// <summary>A button that runs <see cref="IPanelSection.HandleAsync"/> of a section with these arguments.</summary>
    public static InlineKeyboardButton Action(string text, PanelContext context, string sectionId, params string[] args) =>
        InlineKeyboardButton.WithCallbackData(text, Data("s", context.ChatId.ToString(), sectionId, string.Join(':', args)));

    /// <summary>Opens (or refreshes) a section.</summary>
    public static InlineKeyboardButton Open(string text, long chatId, string sectionId) =>
        InlineKeyboardButton.WithCallbackData(text, Data("s", chatId.ToString(), sectionId));

    /// <summary>A button that asks the admin to type text for <paramref name="field"/>; see <see cref="IPanelTextInput"/>.</summary>
    public static InlineKeyboardButton AskText(string text, PanelContext context, string sectionId, string field) =>
        InlineKeyboardButton.WithCallbackData(text, Data("i", context.ChatId.ToString(), sectionId, field));

    /// <summary>Back to the group's menu.</summary>
    public static InlineKeyboardButton BackToGroup(PanelContext context) =>
        InlineKeyboardButton.WithCallbackData(context.Lang == Languages.English ? "🔙 Back" : "🔙 بازگشت",
            Data("g", context.ChatId.ToString()));

    /// <summary>Switches the section's whole plugin on or off: green when on, red when off.</summary>
    public static InlineKeyboardButton PluginSwitch(PanelContext context, string sectionId)
    {
        var fa = context.Lang != Languages.English;
        var text = context.PluginEnabled
            ? (fa ? "✅ روشن — زدن برای خاموش کردن" : "✅ On — tap to turn off")
            : (fa ? "❌ خاموش — زدن برای روشن کردن" : "❌ Off — tap to turn on");
        return Styled(InlineKeyboardButton.WithCallbackData(text, Data("t", context.ChatId.ToString(), sectionId)),
            context.PluginEnabled ? KeyboardButtonStyle.Success : KeyboardButtonStyle.Danger);
    }

    /// <summary>
    /// One option of a group of choices (a level, a duration…). The chosen one is green and ticked: the colour for
    /// current Telegram apps, the tick for older ones that show every button the same.
    /// </summary>
    public static InlineKeyboardButton Choice(string text, bool chosen, PanelContext context, string sectionId, params string[] args) =>
        Styled(Action(chosen ? "✅ " + text : text, context, sectionId, args), chosen ? KeyboardButtonStyle.Success : null);

    /// <summary>A yes/no setting: green with ✅ when on, plain with ❌ when off.</summary>
    public static InlineKeyboardButton Toggle(string text, bool on, PanelContext context, string sectionId, params string[] args) =>
        Styled(Action((on ? "✅ " : "❌ ") + text, context, sectionId, args), on ? KeyboardButtonStyle.Success : null);

    /// <summary>An action that removes something (delete all, remove rules): red.</summary>
    public static InlineKeyboardButton Danger(string text, PanelContext context, string sectionId, params string[] args) =>
        Styled(Action(text, context, sectionId, args), KeyboardButtonStyle.Danger);

    /// <summary>The main action of a screen (write a text, add words): blue.</summary>
    public static InlineKeyboardButton Primary(InlineKeyboardButton button) => Styled(button, KeyboardButtonStyle.Primary);

    /// <summary>
    /// A title row above a group of buttons («⏱ مدت سکوت»), so it is clear which buttons belong to which setting.
    /// Pressing it does nothing.
    /// </summary>
    public static InlineKeyboardButton[] Header(string text) =>
        [InlineKeyboardButton.WithCallbackData($"┈┈ {text} ┈┈", Data("n"))];

    /// <summary>"✅ " in front of the option that is currently chosen.</summary>
    public static string Mark(bool chosen, string text) => chosen ? "✅ " + text : text;

    private static InlineKeyboardButton Styled(InlineKeyboardButton button, KeyboardButtonStyle? style)
    {
        button.Style = style;
        return button;
    }

    public static string Data(params string[] parts) =>
        Prefix + ":" + string.Join(':', parts.Where(p => p.Length > 0));
}
