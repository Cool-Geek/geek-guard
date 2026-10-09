using System.Collections.Concurrent;
using Dapper;
using Npgsql;
using Telegram.Bot.Types;

namespace GeekGuard.Core.Groups;

/// <summary>A member the bot has seen in a group.</summary>
/// <remarks>Dapper fills records through the constructor, so queries alias columns to these names, in this order.</remarks>
public sealed record KnownMember(long UserId, string FirstName, string? Username)
{
    /// <summary>A Telegram user object for mentions and actions.</summary>
    public User ToUser() => new() { Id = UserId, FirstName = FirstName, Username = Username };
}

/// <summary>
/// Remembers who has been seen in each group (id, name, @username, last seen; never message text), so admins
/// can name a member by @username. Telegram offers no way to turn a @username into an id, so this is the only way.
/// </summary>
public sealed class MemberDirectory(NpgsqlDataSource db, TimeProvider clock)
{
    /// <summary>A member who keeps talking is written again at most this often.</summary>
    private static readonly TimeSpan RefreshEvery = TimeSpan.FromHours(1);

    private const long TelegramServiceId = 777000;

    private readonly ConcurrentDictionary<(long Chat, long User), (string? Username, string FirstName, DateTimeOffset At)> _written = new();

    /// <summary>Records that <paramref name="user"/> was seen in the group. Cheap to call for every message.</summary>
    public async Task SeeAsync(long chatId, User user, CancellationToken ct = default)
    {
        if (user.Id is GroupAdmins.AnonymousAdminId or TelegramServiceId) return;

        var now = clock.GetUtcNow();
        var key = (chatId, user.Id);
        if (_written.TryGetValue(key, out var last)
            && last.Username == user.Username && last.FirstName == user.FirstName && now - last.At < RefreshEvery)
            return;

        await using var connection = await db.OpenConnectionAsync(ct);
        await connection.ExecuteAsync(new CommandDefinition(
            """
            INSERT INTO group_members (chat_id, user_id, first_name, username)
            VALUES (@chatId, @userId, @firstName, @username)
            ON CONFLICT (chat_id, user_id) DO UPDATE
               SET first_name   = EXCLUDED.first_name,
                   username     = EXCLUDED.username,
                   last_seen_at = now()
            """,
            new { chatId, userId = user.Id, firstName = user.FirstName, username = user.Username },
            cancellationToken: ct));
        _written[key] = (user.Username, user.FirstName, now);
    }

    /// <summary>The member with this @username (with or without the @), most recently seen first.</summary>
    public async Task<KnownMember?> FindByUsernameAsync(long chatId, string username, CancellationToken ct = default)
    {
        await using var connection = await db.OpenConnectionAsync(ct);
        return await connection.QueryFirstOrDefaultAsync<KnownMember>(new CommandDefinition(
            """
            SELECT user_id AS UserId, first_name AS FirstName, username AS Username
              FROM group_members
             WHERE chat_id = @chatId AND lower(username) = lower(@username)
             ORDER BY last_seen_at DESC
             LIMIT 1
            """,
            new { chatId, username = username.TrimStart('@') },
            cancellationToken: ct));
    }

    public async Task<KnownMember?> FindAsync(long chatId, long userId, CancellationToken ct = default)
    {
        await using var connection = await db.OpenConnectionAsync(ct);
        return await connection.QueryFirstOrDefaultAsync<KnownMember>(new CommandDefinition(
            """
            SELECT user_id AS UserId, first_name AS FirstName, username AS Username
              FROM group_members
             WHERE chat_id = @chatId AND user_id = @userId
            """,
            new { chatId, userId },
            cancellationToken: ct));
    }
}
