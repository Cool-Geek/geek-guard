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

    /// <summary>Shows only the bot id part of a token, so logs never leak the secret half.</summary>
    public static string Mask(string token)
    {
        var colon = token.IndexOf(':');
        return colon > 0 ? $"{token[..colon]}:***" : "***";
    }
}
