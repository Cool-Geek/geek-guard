using Dapper;
using Npgsql;

namespace GeekGuard.Core.Users;

/// <summary>A person who has talked to the bot in a private chat.</summary>
/// <remarks>Dapper fills records through the constructor, so queries alias columns to these names, in this order.</remarks>
public sealed record BotUser(long UserId, string FirstName, string? Username, string? LanguageCode, string? Lang);

public sealed class UserRepository(NpgsqlDataSource db)
{
    /// <summary>Creates the user on first contact, or refreshes their profile and last-seen time.</summary>
    public async Task<BotUser> UpsertAsync(long userId, string firstName, string? username, string? languageCode,
        CancellationToken ct = default)
    {
        await using var connection = await db.OpenConnectionAsync(ct);
        return await connection.QuerySingleAsync<BotUser>(new CommandDefinition(
            """
            INSERT INTO users (user_id, first_name, username, language_code)
            VALUES (@userId, @firstName, @username, @languageCode)
            ON CONFLICT (user_id) DO UPDATE
               SET first_name    = EXCLUDED.first_name,
                   username      = EXCLUDED.username,
                   language_code = EXCLUDED.language_code,
                   last_seen_at  = now()
            RETURNING user_id       AS UserId,
                      first_name    AS FirstName,
                      username      AS Username,
                      language_code AS LanguageCode,
                      lang          AS Lang
            """,
            new { userId, firstName, username, languageCode },
            cancellationToken: ct));
    }

    public async Task<int> CountAsync(CancellationToken ct = default)
    {
        await using var connection = await db.OpenConnectionAsync(ct);
        return await connection.ExecuteScalarAsync<int>(new CommandDefinition("SELECT count(*) FROM users", cancellationToken: ct));
    }
}
