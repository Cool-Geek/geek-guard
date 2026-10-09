using Dapper;
using Npgsql;

namespace GeekGuard.Modules.Moderation;

/// <summary>Warning counts per member per group. Only the numeric user id is stored.</summary>
public sealed class WarnStore(NpgsqlDataSource db)
{
    /// <summary>Adds one warning and returns the new total.</summary>
    public async Task<int> AddAsync(long chatId, long userId, CancellationToken ct = default)
    {
        await using var connection = await db.OpenConnectionAsync(ct);
        return await connection.ExecuteScalarAsync<int>(new CommandDefinition(
            """
            INSERT INTO warnings (chat_id, user_id, count) VALUES (@chatId, @userId, 1)
            ON CONFLICT (chat_id, user_id) DO UPDATE SET count = warnings.count + 1, updated_at = now()
            RETURNING count
            """,
            new { chatId, userId }, cancellationToken: ct));
    }

    /// <summary>Clears a member's warnings. Returns how many they had.</summary>
    public async Task<int> ResetAsync(long chatId, long userId, CancellationToken ct = default)
    {
        await using var connection = await db.OpenConnectionAsync(ct);
        return await connection.ExecuteScalarAsync<int?>(new CommandDefinition(
            "DELETE FROM warnings WHERE chat_id = @chatId AND user_id = @userId RETURNING count",
            new { chatId, userId }, cancellationToken: ct)) ?? 0;
    }
}
