namespace GeekGuard.Bot.Licensing;

/// <summary>
/// Connection to the Geek Forge shop, which sells Pro and plugin activation codes. Bound from "GeekForge".
/// Both values are optional: without them /activate answers that activation is unavailable and the bot runs as usual.
/// </summary>
public sealed class GeekForgeOptions
{
    public const string SectionName = "GeekForge";

    /// <summary>The shop's address, e.g. "https://geekforge.ir" (or "https://localhost:7001" while developing).</summary>
    public string BaseUrl { get; set; } = "";

    /// <summary>
    /// Shared secret, the same value as "BotApi:Key" on the shop. Secret: keep it in User Secrets or an
    /// environment variable (GeekForge__ApiKey), never in appsettings.json.
    /// </summary>
    public string ApiKey { get; set; } = "";

    public bool IsConfigured =>
        Uri.TryCreate(BaseUrl, UriKind.Absolute, out var uri) && uri.Scheme is "https" or "http"
        && ApiKey.Trim().Length >= 24;
}
