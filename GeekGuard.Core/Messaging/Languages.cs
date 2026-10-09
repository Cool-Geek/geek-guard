namespace GeekGuard.Core.Messaging;

/// <summary>The languages the bot speaks.</summary>
public static class Languages
{
    public const string Persian = "fa";
    public const string English = "en";

    /// <summary>Picks the bot language from a Telegram language_code: Persian for "fa…", English otherwise.</summary>
    public static string FromTelegram(string? languageCode) =>
        languageCode is not null && languageCode.StartsWith("fa", StringComparison.OrdinalIgnoreCase) ? Persian : English;

    /// <summary>
    /// Language for a group the bot has just joined: Persian when the group's title is written in Persian
    /// or Arabic script, otherwise the language of the person who added the bot. Admins can change it later.
    /// </summary>
    public static string ForNewGroup(string? title, string? adderLanguageCode) =>
        title is not null && title.Any(c => c is >= '\u0600' and <= '\u06FF') ? Persian : FromTelegram(adderLanguageCode);
}
