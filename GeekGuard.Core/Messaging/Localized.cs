namespace GeekGuard.Core.Messaging;

/// <summary>
/// One piece of user-facing text in both languages. Placeholders follow string.Format: {0}, {1}…
/// </summary>
/// <remarks>
/// The text comes back with its direction marked on every line (see <see cref="Bidi"/>), so a Persian line that
/// starts with an emoji or a Latin name still reads right to left, and an English line with a Persian name
/// still reads left to right.
/// </remarks>
/// <example><c>static readonly Localized Banned = new("⛔ {0} بن شد.", "⛔ {0} was banned.");</c></example>
public readonly record struct Localized(string Fa, string En)
{
    public string Get(string lang) => Bidi.Mark(lang == Languages.English ? En : Fa, lang);

    public string Format(string lang, params object?[] args) => string.Format(Get(lang), args);

    /// <summary>The text without direction marks, for small pieces placed inside another line («۲ ساعت»).</summary>
    public string Raw(string lang) => lang == Languages.English ? En : Fa;
}

/// <summary>
/// Text direction. Telegram picks each line's direction from its first letter, so «⛔ Ali بن شد» comes out
/// left to right and jumbled. An invisible mark at the start of each line fixes the direction: RLM (U+200F) for
/// Persian, LRM (U+200E) for English.
/// </summary>
public static class Bidi
{
    public const char RightToLeft = '‏';
    public const char LeftToRight = '‎';

    /// <summary>Starts every line of <paramref name="text"/> with the direction mark of <paramref name="lang"/>.</summary>
    public static string Mark(string text, string lang)
    {
        if (string.IsNullOrEmpty(text)) return text;
        var mark = lang == Languages.English ? LeftToRight : RightToLeft;
        return mark + text.Replace("\n", "\n" + mark);
    }
}
