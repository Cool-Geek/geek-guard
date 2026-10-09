using Telegram.Bot.Types;

namespace GeekGuard.Core.Callbacks;

/// <summary>
/// Handles presses of inline buttons whose data starts with <see cref="Prefix"/> and a colon, e.g. "rp:ban:42".
/// Register with <c>services.AddCallbackHandler&lt;T&gt;()</c>.
/// </summary>
/// <remarks>
/// Button data is at most 64 bytes and anyone who can see a button can press it, so handlers keep only ids in it
/// and always re-check that the person pressing is allowed to do what the button does.
/// </remarks>
public interface ICallbackHandler
{
    /// <summary>Short, unique prefix, e.g. "rp" for reports.</summary>
    string Prefix { get; }

    /// <param name="args">The data after the prefix, split on ':'.</param>
    Task HandleAsync(CallbackQuery query, string[] args, CancellationToken ct);
}
