namespace GeekGuard.Core.Messaging;

/// <summary>The languages the bot speaks.</summary>
public static class Languages
{
    public const string Persian = "fa";
    public const string English = "en";

    /// <summary>Picks the bot language from a Telegram language_code: Persian for "fa…", English otherwise.</summary>
    public static string FromTelegram(string? languageCode) =>
        languageCode is not null && languageCode.StartsWith("fa", StringComparison.OrdinalIgnoreCase) ? Persian : English;
}
