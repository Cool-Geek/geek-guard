using Dapper;
using GeekGuard.Bot.Configuration;
using GeekGuard.Core.Groups;
using GeekGuard.Core.Messaging;
using GeekGuard.Core.Plugins;
using Microsoft.Extensions.Options;
using Npgsql;
using Telegram.Bot.Types;

namespace GeekGuard.Bot.Owner;

/// <summary>
/// Commands for the bot's owner in the private chat: /pro to give a group Pro, /stats for the numbers.
/// Anyone else gets no answer at all, as if the commands did not exist. They never appear in the "/" menu.
/// </summary>
public sealed class OwnerCommands(
    IOptions<TelegramOptions> options,
    GroupDirectory groups,
    PluginStateStore states,
    NpgsqlDataSource db,
    BotActions actions,
    TimeProvider clock)
{
    private const string ProUsage =
        "💎 <code>/pro &lt;آیدی گروه&gt; &lt;روز&gt;</code>\nمثلاً <code>/pro -1001234567890 30</code> — با ۰ روز Pro لغو می‌شود.\nآیدی هر گروه در پنل، صفحه‌ی همان گروه، هست.";

    /// <summary>Handles the message if it is an owner command from an owner. Returns false otherwise.</summary>
    public async Task<bool> TryHandleAsync(Message message, CancellationToken ct)
    {
        if (message is not { Text: { } text, From: { } from }) return false;

        var parts = text.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var command = parts[0].Split('@')[0].ToLowerInvariant();
        if (command is not ("/pro" or "/stats")) return false;

        // Not an owner: swallow the command silently.
        if (!options.Value.OwnerIds.Contains(from.Id)) return true;

        var reply = command == "/pro" ? await ProAsync(parts, ct) : await StatsAsync(ct);
        await actions.SendAsync(message.Chat.Id, reply, ct: ct);
        return true;
    }

    private async Task<string> ProAsync(string[] parts, CancellationToken ct)
    {
        if (parts.Length != 3 || !long.TryParse(parts[1], out var chatId) || !int.TryParse(parts[2], out var days) || days is < 0 or > 3650)
            return ProUsage;

        if (await groups.GetAsync(chatId, ct) is not { } group)
            return $"❓ گروهی با آیدی <code>{chatId}</code> نمی‌شناسم. ربات باید عضو آن گروه باشد.";

        var title = Html.Escape(group.Title);
        if (days == 0)
        {
            await states.SetProUntilAsync(chatId, null, ct);
            return $"✅ Pro گروه «{title}» لغو شد.";
        }

        // Days are added to what is left, so renewing early never loses time.
        var now = clock.GetUtcNow().UtcDateTime;
        var current = await states.GetProUntilAsync(chatId, ct);
        var from = current > now ? current.Value : now;
        var until = from.AddDays(days);
        await states.SetProUntilAsync(chatId, until, ct);

        return $"💎 Pro گروه «{title}» فعال شد تا <b>{LocalTime.When(new DateTimeOffset(until, TimeSpan.Zero), Languages.Persian, clock)}</b>.";
    }

    private async Task<string> StatsAsync(CancellationToken ct)
    {
        await using var connection = await db.OpenConnectionAsync(ct);
        var row = await connection.QuerySingleAsync<(long Groups, long Pro, long Users, long Members, long Closed)>(new CommandDefinition(
            """
            SELECT (SELECT count(*) FROM groups WHERE active),
                   (SELECT count(*) FROM groups WHERE active AND pro_until > now()),
                   (SELECT count(*) FROM users),
                   (SELECT count(DISTINCT user_id) FROM group_members),
                   (SELECT count(*) FROM group_closures)
            """, cancellationToken: ct));

        return $"""
            📊 <b>آمار Geek Guard</b>

            👥 گروه‌های فعال: <b>{row.Groups}</b>
            💎 گروه‌های Pro: <b>{row.Pro}</b>
            🔒 گروه‌های بسته در این لحظه: <b>{row.Closed}</b>
            🙋 کسانی که ربات را استارت کرده‌اند: <b>{row.Users}</b>
            👤 افراد دیده‌شده در گروه‌ها: <b>{row.Members}</b>
            """;
    }
}
