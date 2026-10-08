using System.Text.RegularExpressions;

namespace CoolGeek.PersianText;

/// <summary>Finds links and @usernames, including the disguises spammers use in Persian and English.</summary>
public static partial class LinkDetector
{
    // http(s)://, www., t.me/…, telegram.me/…, telegram.dog/…, tg://
    [GeneratedRegex(@"(https?://|www\.|\b(t|telegram)\.(me|dog)/|tg://)",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex PlainLink();

    // Bare domains on common TLDs: example.com, site.ir/page
    [GeneratedRegex(@"\b[a-z0-9][a-z0-9\-]{1,62}\.(com|ir|net|org|io|me|xyz|info|site|link|top|shop|app|online|store|club|vip|live|pro|cc|co|tk|ru|biz|click|website|space|fun)\b",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex BareDomain();

    // Disguised Telegram links: "t . me / x", "t(.)me", "t[dot]me", "تی دات می", "t نقطه me"
    [GeneratedRegex(@"\b(t|telegram|تی)\s*[\(\[\{]?\s*(\.|dot|دات|نقطه|٫)\s*[\)\]\}]?\s*(me|می)\b\s*[/\\]?",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex DisguisedTelegram();

    // Invite links that survive without a domain: "joinchat/…", "+AbCdEf123456"
    [GeneratedRegex(@"\bjoinchat\b|(^|\s)\+[a-z0-9_\-]{12,}\b",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex InviteFragment();

    // Telegram usernames: 5–32 characters, start with a letter; not the middle of an e-mail address
    [GeneratedRegex(@"(^|[^a-z0-9_])@[a-z][a-z0-9_]{4,31}\b",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex Username();

    /// <summary>True if the text contains a link, plain or disguised.</summary>
    public static bool ContainsLink(NormalizedText text) =>
        !text.IsEmpty && (PlainLink().IsMatch(text.Value)
                          || DisguisedTelegram().IsMatch(text.Value)
                          || BareDomain().IsMatch(text.Value)
                          || InviteFragment().IsMatch(text.Value));

    /// <inheritdoc cref="ContainsLink(NormalizedText)"/>
    public static bool ContainsLink(string? raw) => ContainsLink(NormalizedText.From(raw));

    /// <summary>True if the text mentions a @username (a channel, group or user).</summary>
    public static bool ContainsUsername(NormalizedText text) => !text.IsEmpty && Username().IsMatch(text.Value);

    /// <inheritdoc cref="ContainsUsername(NormalizedText)"/>
    public static bool ContainsUsername(string? raw) => ContainsUsername(NormalizedText.From(raw));
}
