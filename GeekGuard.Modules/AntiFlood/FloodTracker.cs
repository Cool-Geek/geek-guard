using System.Collections.Concurrent;

namespace GeekGuard.Modules.AntiFlood;

/// <summary>
/// Counts each member's messages in a sliding window. In memory: right for a single bot instance;
/// it moves to Redis if the bot ever runs on several machines.
/// </summary>
public sealed class FloodTracker(TimeProvider clock)
{
    private readonly ConcurrentDictionary<(long Chat, long User), Queue<(DateTimeOffset At, int MessageId)>> _recent = new();
    private DateTimeOffset _lastSweep = DateTimeOffset.MinValue;

    /// <summary>
    /// Records a message. When the member reaches <paramref name="limit"/> messages within <paramref name="window"/>,
    /// returns the ids of all those messages (so the whole burst can be removed) and starts counting afresh.
    /// Otherwise returns null.
    /// </summary>
    public IReadOnlyList<int>? Hit(long chatId, long userId, int messageId, int limit, TimeSpan window)
    {
        var now = clock.GetUtcNow();
        var queue = _recent.GetOrAdd((chatId, userId), _ => new Queue<(DateTimeOffset, int)>());

        IReadOnlyList<int>? burst = null;
        lock (queue)
        {
            while (queue.Count > 0 && now - queue.Peek().At > window) queue.Dequeue();
            queue.Enqueue((now, messageId));

            if (queue.Count >= limit)
            {
                burst = queue.Select(entry => entry.MessageId).ToArray();
                queue.Clear(); // punish once per burst, not once per message after the limit
            }
        }

        if (now - _lastSweep > TimeSpan.FromMinutes(5)) Sweep(now, window);
        return burst;
    }

    /// <summary>Drops members who have gone quiet, so memory does not grow with every member ever seen.</summary>
    private void Sweep(DateTimeOffset now, TimeSpan window)
    {
        _lastSweep = now;
        foreach (var (key, queue) in _recent)
        {
            lock (queue)
            {
                if (queue.Count == 0 || now - queue.Last().At > window) _recent.TryRemove(key, out _);
            }
        }
    }
}
