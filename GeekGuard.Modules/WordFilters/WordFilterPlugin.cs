using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using CoolGeek.PersianText;
using GeekGuard.Core.Messaging;
using GeekGuard.Modules.Filters;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace GeekGuard.Modules.WordFilters;

/// <summary>
/// Banned words chosen by each group's admins. Messages from members that contain one are removed, even when the
/// word is disguised ("ت.ب.ل.ی.غ", "تبلیـــغ", Arabic letters, repeated letters).
/// </summary>
public sealed class WordFilterPlugin : IGeekGuardPlugin
{
    public const string Id = "word-filter";

    public PluginManifest Manifest { get; } = new(Id, "فیلتر کلمات", "Word filter", PluginTier.Free, EnabledByDefault: true);

    public void ConfigureServices(IServiceCollection services, IConfiguration configuration)
    {
        services.TryAddSingleton<ViolationService>();
        services.AddGroupMessageHandler<WordFilterCommandsHandler>();
        services.AddGroupMessageHandler<WordFilterHandler>();
        services.AddPanelSection<WordFilterSection>();
        services.AddHelpTopic(Help.HelpTopics.WordFilter);
    }
}

/// <summary>Per-group banned words.</summary>
public sealed class WordFilterSettings
{
    /// <summary>Words a group can ban on the free plan.</summary>
    public const int FreeLimit = 20;

    /// <summary>Longest word or phrase accepted.</summary>
    public const int MaxWordLength = 64;

    /// <summary>The words as the admin typed them (shown back in the list).</summary>
    public List<string> Words { get; set; } = [];

    /// <summary>Warn members who use a banned word (counts toward the group's warning limit).</summary>
    public bool WarnOnViolation { get; set; } = true;
}

/// <summary>Removes member messages that contain a banned word.</summary>
public sealed class WordFilterHandler(PluginStateStore states, ViolationService violations) : IGroupMessageHandler
{
    private static readonly Localized Reason = new(
        "پیامت به خاطر کلمه‌ی نامناسب پاک شد.", "your message was removed for a banned word.");

    // Settings objects are cached and replaced (never changed) on save, so a filter built for one
    // settings object stays valid for as long as that object lives.
    private static readonly ConditionalWeakTable<WordFilterSettings, WordFilter> Filters = new();

    public string PluginId => WordFilterPlugin.Id;

    public int Order => HandlerOrder.ContentFilters;

    public async Task<HandlerResult> HandleAsync(GroupMessageContext context, CancellationToken ct)
    {
        if (context.SenderIsAdmin || context.NormalizedText.IsEmpty) return HandlerResult.Continue;

        var settings = await states.GetSettingsAsync<WordFilterSettings>(context.ChatId, WordFilterPlugin.Id, ct);
        if (settings.Words.Count == 0) return HandlerResult.Continue;

        var filter = Filters.GetValue(settings, s => new WordFilter(s.Words));
        if (filter.FindMatch(context.NormalizedText) is null) return HandlerResult.Continue;

        // The reason never repeats the word: the bot should not post the very word it just removed.
        await violations.HandleAsync(context, Reason, settings.WarnOnViolation, WordFilterPlugin.Id, ct);
        return HandlerResult.Stop;
    }
}

/// <summary>
/// Admins only:
/// /filter word1, word2 — or «فیلتر کلمه X»; /unfilter X — or «حذف فیلتر X»; /unfilter all — or «حذف فیلتر همه»;
/// /filters — or «فیلترها» / «لیست فیلتر».
/// </summary>
public sealed class WordFilterCommandsHandler(PluginStateStore states, BotActions actions) : IGroupMessageHandler
{
    private static readonly string[] AddPrefixes = Normalized("فیلتر کلمه", "فیلتر کلمات");
    private static readonly string[] RemovePrefixes = Normalized("حذف فیلتر", "رفع فیلتر");
    private static readonly string[] ListWords = Normalized("فیلترها", "فیلتر ها", "لیست فیلتر", "لیست فیلترها", "کلمات فیلتر");
    private static readonly string[] AllWords = Normalized("all", "همه");

    private static readonly Localized ListTitle = new("🚫 کلمه‌های فیلترشده ({0} از {1}):", "🚫 Banned words ({0} of {1}):");
    private static readonly Localized Empty = new("هنوز کلمه‌ای فیلتر نشده.", "No banned words yet.");
    private static readonly Localized Help = new(
        "افزودن: «فیلتر کلمه X» یا /filter X\nحذف: «حذف فیلتر X» یا /unfilter X\nچند کلمه را با «،» جدا کن.",
        "Add: /filter X — Remove: /unfilter X — separate several with commas.");
    private static readonly Localized Added = new("✅ فیلتر شد: {0}\n({1} از {2})", "✅ Banned: {0}\n({1} of {2})");
    private static readonly Localized AlreadyThere = new("ℹ️ این کلمه‌ها از قبل فیلتر بودند.", "ℹ️ Those words were already banned.");
    private static readonly Localized Removed = new("🗑 از فیلتر برداشته شد: {0}", "🗑 Removed: {0}");
    private static readonly Localized NotThere = new("ℹ️ این کلمه‌ها در فیلتر نبودند.", "ℹ️ Those words were not banned.");
    private static readonly Localized Cleared = new("🗑 همه‌ی کلمه‌های فیلتر پاک شدند.", "🗑 All banned words removed.");
    private static readonly Localized LimitReached = new(
        "⚠️ نسخه‌ی رایگان حداکثر {0} کلمه دارد. اول چند کلمه را حذف کن.",
        "⚠️ The free plan allows up to {0} words. Remove some first.");
    private static readonly Localized TooLong = new(
        "⚠️ هر کلمه حداکثر {0} حرف می‌تواند باشد.", "⚠️ Each word can be at most {0} characters.");

    private static readonly TimeSpan Lifetime = TimeSpan.FromSeconds(30);

    private enum Verb { List, Add, Remove }

    public string PluginId => WordFilterPlugin.Id;

    public int Order => HandlerOrder.Commands;

    public async Task<HandlerResult> HandleAsync(GroupMessageContext context, CancellationToken ct)
    {
        var request = ReadRequest(context);
        if (request is null) return HandlerResult.Continue;
        if (!context.SenderIsAdmin) return HandlerResult.Continue;

        var (verb, words, fromKeyword) = request.Value;
        var chatId = context.ChatId;
        var lang = context.Group.Lang;
        var settings = await states.GetSettingsAsync<WordFilterSettings>(chatId, WordFilterPlugin.Id, ct);

        // «حذف فیلتر …» in ordinary chat (no such word in the list) is just chat: stay quiet.
        if (fromKeyword && verb == Verb.Remove && !IsAll(words) && !settings.Words.Any(w => words.Select(Key).Contains(Key(w))))
            return HandlerResult.Continue;

        // The command itself contains the banned word, so it goes away with the reply.
        actions.ScheduleDelete(chatId, context.Message.MessageId, Lifetime);

        var reply = verb switch
        {
            Verb.Add when words.Count > 0 => await AddAsync(states, chatId, lang, settings, words, ct),
            Verb.Remove when words.Count > 0 => await RemoveAsync(chatId, lang, settings, words, ct),
            _ => ListText(settings, lang),
        };
        await actions.SendTemporaryAsync(chatId, reply, verb == Verb.List ? TimeSpan.FromMinutes(1) : Lifetime, ct: ct);
        return HandlerResult.Stop;
    }

    /// <summary>Adds words to a group's filter (shared with the settings panel). Returns the reply for the admin.</summary>
    internal static async Task<string> AddAsync(PluginStateStore states, long chatId, string lang, WordFilterSettings settings,
        List<string> words, CancellationToken ct)
    {
        if (words.Any(w => w.Length > WordFilterSettings.MaxWordLength))
            return TooLong.Format(lang, WordFilterSettings.MaxWordLength);

        // Two spellings of the same word («تبليغ», «تبلیغ») count once.
        var known = settings.Words.Select(Key).ToHashSet();
        var fresh = words.Where(w => !string.IsNullOrEmpty(Key(w)))
                         .DistinctBy(Key)
                         .Where(w => !known.Contains(Key(w)))
                         .ToList();
        if (fresh.Count == 0) return AlreadyThere.Get(lang);

        if (settings.Words.Count + fresh.Count > WordFilterSettings.FreeLimit)
            return LimitReached.Format(lang, WordFilterSettings.FreeLimit);

        var updated = settings.Words.Concat(fresh).ToList();
        await states.SaveSettingsAsync(chatId, WordFilterPlugin.Id,
            new WordFilterSettings { Words = updated, WarnOnViolation = settings.WarnOnViolation }, ct);
        return Added.Format(lang, Spoilers(fresh), updated.Count, WordFilterSettings.FreeLimit);
    }

    private async Task<string> RemoveAsync(long chatId, string lang, WordFilterSettings settings, List<string> words, CancellationToken ct)
    {
        var clearAll = IsAll(words);
        var keys = words.Select(Key).ToHashSet();
        var removed = clearAll ? settings.Words : settings.Words.Where(w => keys.Contains(Key(w))).ToList();
        if (removed.Count == 0) return NotThere.Get(lang);

        var updated = clearAll ? new List<string>() : settings.Words.Except(removed).ToList();
        await states.SaveSettingsAsync(chatId, WordFilterPlugin.Id,
            new WordFilterSettings { Words = updated, WarnOnViolation = settings.WarnOnViolation }, ct);
        return clearAll ? Cleared.Get(lang) : Removed.Format(lang, Spoilers(removed));
    }

    private static bool IsAll(List<string> words) => words.Count == 1 && AllWords.Contains(PersianNormalizer.Normalize(words[0]));

    private static string ListText(WordFilterSettings settings, string lang)
    {
        var title = ListTitle.Format(lang, settings.Words.Count, WordFilterSettings.FreeLimit);
        var body = settings.Words.Count == 0 ? Empty.Get(lang) : Spoilers(settings.Words);
        return $"<b>{title}</b>\n{body}\n\n{Help.Get(lang)}";
    }

    /// <summary>Banned words are hidden behind a spoiler, so the list itself does not show them to everyone.</summary>
    internal static string Spoilers(IEnumerable<string> words) =>
        string.Join("، ", words.Select(w => $"<tg-spoiler>{Html.Escape(w)}</tg-spoiler>"));

    /// <summary>What the admin asked for, or null if the message is not a filter command.</summary>
    private static (Verb Verb, List<string> Words, bool FromKeyword)? ReadRequest(GroupMessageContext context)
    {
        if (context.Command is { } command)
        {
            var args = SplitWords(command.Args);
            return command.Name switch
            {
                "filters" => (Verb.List, [], false),
                "filter" => (args.Count == 0 ? Verb.List : Verb.Add, args, false),
                "unfilter" => (args.Count == 0 ? Verb.List : Verb.Remove, args, false),
                _ => null,
            };
        }

        var text = context.NormalizedText.Value;
        if (ListWords.Contains(text)) return (Verb.List, [], true);

        // Keywords: words are read from the original text, so the list shows them as the admin typed them.
        var raw = context.Message.Text ?? "";
        if (AddPrefixes.FirstOrDefault(p => text.StartsWith(p + " ", StringComparison.Ordinal)) is { } add)
            return (Verb.Add, SplitWords(SkipWords(raw, WordCount(add))), true);
        if (RemovePrefixes.FirstOrDefault(p => text.StartsWith(p + " ", StringComparison.Ordinal)) is { } remove)
            return (Verb.Remove, SplitWords(SkipWords(raw, WordCount(remove))), true);
        return null;
    }

    /// <summary>
    /// Entries are separated by commas («،» or ",") or new lines. Without separators the whole text is one entry,
    /// so "/filter خیلی بد" bans the phrase, not the short word «بد» on its own.
    /// </summary>
    internal static List<string> SplitWords(string text) =>
        text.Split([',', '،', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(w => Regex.Replace(w, @"\s+", " "))
            .ToList();

    /// <summary>The text after its first <paramref name="count"/> words, line breaks kept.</summary>
    private static string SkipWords(string text, int count) =>
        Regex.Replace(text, $@"^\s*(\S+\s+){{{count}}}", "");

    private static int WordCount(string phrase) => phrase.Split(' ', StringSplitOptions.RemoveEmptyEntries).Length;

    /// <summary>Spelling-insensitive key used to compare words.</summary>
    private static string Key(string word) => NormalizedText.From(word).Collapsed;

    private static string[] Normalized(params string[] words) => words.Select(PersianNormalizer.Normalize).ToArray();
}
