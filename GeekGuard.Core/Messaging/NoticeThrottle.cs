using System.Collections.Concurrent;

namespace GeekGuard.Core.Messaging;

/// <summary>
/// Limits how often the bot answers because of one member. Without it, a spammer posting twenty links
/// would make the bot post twenty notices: the bot itself would become the spam.
/// </summary>
public sealed class NoticeThrottle(TimeProvider clock)
{
    /// <summary>Default gap between two notices caused by the same member in the same group.</summary>
    public static readonly TimeSpan DefaultWindow = TimeSpan.FromMinutes(1);

    private readonly ConcurrentDictionary<(long Chat, long User, string Kind), DateTimeOffset> _last = new();
    private DateTimeOffset _lastSweep = DateTimeOffset.MinValue;

    /// <summary>
    /// True if a notice of this <paramref name="kind"/> may be sent now; records it if so.
    /// The action itself (deleting the message, warning) always happens; only the notice is skipped.
    /// </summary>
    public bool TryAcquire(long chatId, long userId, string kind, TimeSpan? window = null)
    {
        var now = clock.GetUtcNow();
        var gap = window ?? DefaultWindow;
        var key = (chatId, userId, kind);

        var allowed = true;
        _last.AddOrUpdate(key, now, (_, previous) =>
        {
            if (now - previous < gap)
            {
                allowed = false;
                return previous;
            }
            return now;
        });

        if (now - _lastSweep > TimeSpan.FromMinutes(10)) Sweep(now);
        return allowed;
    }

    /// <summary>Forgets old entries so the dictionary does not grow forever.</summary>
    private void Sweep(DateTimeOffset now)
    {
        _lastSweep = now;
        foreach (var (key, at) in _last)
        {
            if (now - at > TimeSpan.FromHours(1)) _last.TryRemove(key, out _);
        }
    }
}
