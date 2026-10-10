using Dapper;
using Npgsql;

namespace GeekGuard.Modules.Moderation;

/// <summary>Warning counts per member per group. Only the numeric user id is stored.</summary>
public sealed class WarnStore(NpgsqlDataSource db)
{
    /// <summary>
    /// Adds one warning and returns the new total. Warnings older than <paramref name="expiryDays"/> (counted from
    /// the last one) no longer count, so the member starts again from 1. 0 days: they never expire.
    /// </summary>
    public async Task<int> AddAsync(long chatId, long userId, int expiryDays, CancellationToken ct = default)
    {
        await using var connection = await db.OpenConnectionAsync(ct);
        return await connection.ExecuteScalarAsync<int>(new CommandDefinition(
            """
            INSERT INTO warnings (chat_id, user_id, count) VALUES (@chatId, @userId, 1)
            ON CONFLICT (chat_id, user_id) DO UPDATE
               SET count = CASE WHEN @expiryDays > 0 AND warnings.updated_at < now() - make_interval(days => @expiryDays)
                                THEN 1 ELSE warnings.count + 1 END,
                   updated_at = now()
            RETURNING count
            """,
            new { chatId, userId, expiryDays }, cancellationToken: ct));
    }

    /// <summary>Clears a member's warnings. Returns how many still counted (expired ones count as none).</summary>
    public async Task<int> ResetAsync(long chatId, long userId, int expiryDays = 0, CancellationToken ct = default)
    {
        await using var connection = await db.OpenConnectionAsync(ct);
        return await connection.ExecuteScalarAsync<int?>(new CommandDefinition(
            """
            DELETE FROM warnings WHERE chat_id = @chatId AND user_id = @userId
            RETURNING CASE WHEN @expiryDays > 0 AND updated_at < now() - make_interval(days => @expiryDays) THEN 0 ELSE count END
            """,
            new { chatId, userId, expiryDays }, cancellationToken: ct)) ?? 0;
    }
}
