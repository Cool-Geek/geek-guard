using Telegram.Bot.Types;

namespace GeekGuard.Core.Private;

/// <summary>
/// Handles links that open the bot's private chat with a parameter: t.me/geek_guard_bot?start=rules_-100123
/// arrives as "/start rules_-100123" and goes to the handler whose <see cref="Prefix"/> is "rules".
/// Register with <c>services.AddStartLinkHandler&lt;T&gt;()</c>.
/// </summary>
/// <remarks>
/// Anyone can craft such a link, so handlers treat the argument as untrusted input and only reveal what the
/// person opening it may see.
/// </remarks>
public interface IStartLinkHandler
{
    /// <summary>The part before the first '_', e.g. "rules". Letters and digits only.</summary>
    string Prefix { get; }

    /// <param name="message">The "/start …" message in the private chat.</param>
    /// <param name="argument">The part after the first '_', e.g. "-100123".</param>
    /// <param name="ct">Cancellation.</param>
    Task HandleAsync(Message message, string argument, CancellationToken ct);
}

/// <summary>Builds start links. Telegram allows only A–Z, a–z, 0–9, _ and - in them, up to 64 characters.</summary>
public static class StartLinks
{
    public static string For(string botUsername, string prefix, string argument) =>
        $"https://t.me/{botUsername}?start={prefix}_{argument}";
}
