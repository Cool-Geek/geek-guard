using System.Collections.Concurrent;
using Dapper;
using Npgsql;

namespace GeekGuard.Core.Groups;

/// <summary>A group the bot has been added to.</summary>
/// <param name="Lang">Language of the bot's messages in this group: "fa" or "en".</param>
public sealed record GroupInfo(long ChatId, string Title, bool Active, string Lang);

/// <summary>
/// The groups table with an in-memory cache, because it is consulted on every group message.
/// The cache is per process: fine while the bot runs as a single instance.
/// </summary>
public sealed class GroupDirectory(NpgsqlDataSource db)
{
    private readonly ConcurrentDictionary<long, GroupInfo> _cache = new();

    /// <summary>
    /// Returns the group, creating it on first sight and reactivating it or refreshing its title when needed.
    /// Only touches the database when something changed.
    /// </summary>
    /// <param name="lang">Language for a newly created group; existing groups keep theirs.</param>
    public async Task<GroupInfo> GetOrAddAsync(long chatId, string title, long? addedBy, string lang, CancellationToken ct = default)
    {
        if (_cache.TryGetValue(chatId, out var cached) && cached.Active && cached.Title == title)
            return cached;

        await using var connection = await db.OpenConnectionAsync(ct);
        var group = await connection.QuerySingleAsync<GroupInfo>(new CommandDefinition(
            """
            INSERT INTO groups (chat_id, title, added_by, lang)
            VALUES (@chatId, @title, @addedBy, @lang)
            ON CONFLICT (chat_id) DO UPDATE
               SET title = EXCLUDED.title, active = TRUE, updated_at = now()
            RETURNING chat_id AS ChatId, title AS Title, active AS Active, lang AS Lang
            """,
            new { chatId, title, addedBy, lang },
            cancellationToken: ct));

        _cache[chatId] = group;
        return group;
    }

    /// <summary>A group by id, from the cache when possible. Null if the bot has never been in it.</summary>
    public async Task<GroupInfo?> GetAsync(long chatId, CancellationToken ct = default)
    {
        if (_cache.TryGetValue(chatId, out var cached)) return cached;

        await using var connection = await db.OpenConnectionAsync(ct);
        return await connection.QuerySingleOrDefaultAsync<GroupInfo>(new CommandDefinition(
            "SELECT chat_id AS ChatId, title AS Title, active AS Active, lang AS Lang FROM groups WHERE chat_id = @chatId",
            new { chatId }, cancellationToken: ct));
    }

    /// <summary>Changes the language of the bot's messages in a group.</summary>
    public async Task SetLangAsync(long chatId, string lang, CancellationToken ct = default)
    {
        await using var connection = await db.OpenConnectionAsync(ct);
        await connection.ExecuteAsync(new CommandDefinition(
            "UPDATE groups SET lang = @lang, updated_at = now() WHERE chat_id = @chatId",
            new { chatId, lang }, cancellationToken: ct));
        if (_cache.TryGetValue(chatId, out var group)) _cache[chatId] = group with { Lang = lang };
    }

    /// <summary>Marks a group inactive after the bot was removed. Its settings are kept for a comeback.</summary>
    public async Task DeactivateAsync(long chatId, CancellationToken ct = default)
    {
        await using var connection = await db.OpenConnectionAsync(ct);
        await connection.ExecuteAsync(new CommandDefinition(
            "UPDATE groups SET active = FALSE, updated_at = now() WHERE chat_id = @chatId",
            new { chatId }, cancellationToken: ct));
        _cache.TryRemove(chatId, out _);
    }
}
