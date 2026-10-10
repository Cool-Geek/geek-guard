using GeekGuard.Core.Messaging;
using GeekGuard.Core.Panel;
using Telegram.Bot.Types.ReplyMarkups;

namespace GeekGuard.Modules.WordFilters;

/// <summary>Panel: the banned words, with add, remove and warn options.</summary>
public sealed class WordFilterSection(PluginStateStore states) : IPanelSection, IPanelTextInput
{
    private static readonly Localized Text = new(
        "🚫 <b>فیلتر کلمات</b> ({0} از {1})\n\nپیام اعضا که یکی از این کلمه‌ها را داشته باشد پاک می‌شود، حتی اگر کلمه را با فاصله، نقطه یا حروف کشیده بنویسند.\n\n{2}\n\nاخطار به فرستنده: <b>{3}</b>\n<i>برای حذف یک کلمه، دکمه‌ی 🗑 کنارش را بزن.</i>",
        "🚫 <b>Word filter</b> ({0} of {1})\n\nMember messages containing one of these words are removed, even when the word is spaced out, dotted or stretched.\n\n{2}\n\nWarn the sender: <b>{3}</b>\n<i>Tap 🗑 next to a word to remove it.</i>");
    private static readonly Localized Empty = new("هنوز کلمه‌ای فیلتر نشده.", "No banned words yet.");
    private static readonly Localized AskWords = new(
        "کلمه یا کلمه‌هایی را که می‌خواهی فیلتر شود بفرست. چند کلمه را با «،» یا در خط‌های جدا بنویس.",
        "Send the word or words to ban. Separate several with commas or put each on its own line.");

    public string Id => "words";

    public string PluginId => WordFilterPlugin.Id;

    public int Order => 55;

    public Localized Title { get; } = new("🚫 فیلتر کلمات", "🚫 Word filter");

    public bool HasPluginSwitch => false;

    public async Task<PanelView> ShowAsync(PanelContext context, CancellationToken ct)
    {
        var s = await states.GetSettingsAsync<WordFilterSettings>(context.ChatId, WordFilterPlugin.Id, ct);
        var fa = context.Lang != Languages.English;

        var list = s.Words.Count == 0 ? context.T(Empty) : WordFilterCommandsHandler.Spoilers(s.Words);
        var html = context.T(Text)
            .Replace("{0}", s.Words.Count.ToString())
            .Replace("{1}", WordFilterSettings.FreeLimit.ToString())
            .Replace("{3}", s.WarnOnViolation ? "✅" : "❌")
            .Replace("{2}", list); // last: the words themselves may contain "{3}"

        // The index identifies the word; it is checked against the word's text when pressed (see HandleAsync).
        var rows = new List<InlineKeyboardButton[]>
        {
            new[] { PanelButtons.Primary(PanelButtons.AskText(fa ? "➕ افزودن کلمه" : "➕ Add words", context, Id, "add")) },
        };
        if (s.Words.Count > 0)
        {
            rows.Add(PanelButtons.Header(fa ? "🗑 برای حذف، روی کلمه بزن" : "🗑 Tap a word to remove it"));
            rows.AddRange(s.Words
                .Select((word, index) => PanelButtons.Action("🗑 " + Html.Truncate(word, 20), context, Id, "rm", index.ToString(), Fingerprint(word)))
                .Chunk(2));
            rows.Add(new[] { PanelButtons.Danger(fa ? "🗑 حذف همه" : "🗑 Remove all", context, Id, "clear") });
        }
        rows.Add(PanelButtons.Header(fa ? "⚠️ فرستنده‌ی کلمه" : "⚠️ Sender of a banned word"));
        rows.Add(new[] { PanelButtons.Toggle(fa ? "اخطار بگیرد" : "Gets a warning", s.WarnOnViolation, context, Id, "warn") });
        rows.Add(new[] { PanelButtons.BackToGroup(context) });
        return new PanelView(html, new InlineKeyboardMarkup(rows));
    }

    public async Task<string?> HandleAsync(PanelContext context, string[] args, CancellationToken ct)
    {
        var s = await states.GetSettingsAsync<WordFilterSettings>(context.ChatId, WordFilterPlugin.Id, ct);
        var words = s.Words.ToList();
        var warn = s.WarnOnViolation;

        switch (args[0])
        {
            // Another admin may have changed the list since this panel was drawn: only remove if it is still the same word.
            case "rm" when args.Length == 3 && int.TryParse(args[1], out var index)
                           && index >= 0 && index < words.Count && Fingerprint(words[index]) == args[2]:
                words.RemoveAt(index);
                break;
            case "clear":
                words.Clear();
                break;
            case "warn":
                warn = !warn;
                break;
            default:
                return null;
        }

        await states.SaveSettingsAsync(context.ChatId, WordFilterPlugin.Id, new WordFilterSettings { Words = words, WarnOnViolation = warn }, ct);
        return null;
    }

    public Localized Prompt(string field) => AskWords;

    public async Task<PanelInputResult> AcceptTextAsync(PanelContext context, string field, string text, CancellationToken ct)
    {
        var words = WordFilterCommandsHandler.SplitWords(text);
        if (words.Count == 0) return new PanelInputResult(false, context.T(AskWords));

        var s = await states.GetSettingsAsync<WordFilterSettings>(context.ChatId, WordFilterPlugin.Id, ct);
        var before = s.Words.Count;
        var reply = await WordFilterCommandsHandler.AddAsync(states, context.ChatId, context.Lang, s, words, ct);

        // Saved only when the list grew; otherwise the reply says why (limit, too long, already there) and we ask again.
        var after = (await states.GetSettingsAsync<WordFilterSettings>(context.ChatId, WordFilterPlugin.Id, ct)).Words.Count;
        return new PanelInputResult(after > before, reply);
    }

    /// <summary>
    /// A short check value for a word, so a stale button cannot remove a different word. FNV-1a rather than
    /// GetHashCode, which changes every time the bot starts.
    /// </summary>
    private static string Fingerprint(string word)
    {
        var hash = 2166136261u;
        foreach (var c in word) hash = (hash ^ c) * 16777619u;
        return (hash % 65536).ToString("x");
    }
}
