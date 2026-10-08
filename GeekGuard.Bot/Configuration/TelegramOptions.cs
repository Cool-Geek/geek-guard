namespace GeekGuard.Bot.Configuration;

/// <summary>Settings for talking to the Telegram Bot API. Bound from the "Telegram" configuration section.</summary>
public sealed class TelegramOptions
{
    public const string SectionName = "Telegram";

    /// <summary>The token from @BotFather. Secret: keep it in User Secrets or an environment variable.</summary>
    public string BotToken { get; set; } = "";

    /// <summary>
    /// Optional proxy for reaching api.telegram.org, e.g. "socks5://127.0.0.1:10808".
    /// Needed when the machine cannot reach Telegram directly.
    /// </summary>
    public string? Proxy { get; set; }

    /// <summary>
    /// True when the token has the shape BotFather gives out: digits, a colon, then the secret part.
    /// Catches placeholders and copy-paste mistakes before Telegram is ever called.
    /// </summary>
    public static bool LooksValid(string? token) =>
        token is not null && System.Text.RegularExpressions.Regex.IsMatch(token, @"^\d{5,}:[A-Za-z0-9_-]{30,}$");

    /// <summary>Shows only the bot id part of a token, so logs never leak the secret half.</summary>
    public static string Mask(string token)
    {
        var colon = token.IndexOf(':');
        return colon > 0 ? $"{token[..colon]}:***" : "***";
    }
}
