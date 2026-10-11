using Dapper;
using GeekGuard.Core.Plugins;
using Npgsql;

namespace GeekGuard.Core.Licensing;

/// <summary>
/// Applies an accepted activation code to a group: records the code and adds its days to the group's Pro
/// (or to one plugin's licence) in a single transaction. Days are added to what is left, like the owner's /pro.
/// </summary>
public sealed class LicenseRedemptions(NpgsqlDataSource db, PluginStateStore states)
{
    /// <summary>Result of applying a code.</summary>
    /// <param name="Applied">False when this code had already been applied here before (nothing was added again).</param>
    /// <param name="Until">Until when the group (or plugin) is licensed now.</param>
    public sealed record Result(bool Applied, DateTime? Until);

    public async Task<Result> ApplyAsync(long chatId, LicenseGrant grant, long telegramUserId, CancellationToken ct = default)
    {
        await using var connection = await db.OpenConnectionAsync(ct);
        await using var tx = await connection.BeginTransactionAsync(ct);

        var inserted = await connection.ExecuteAsync(new CommandDefinition(
            """
            INSERT INTO license_redemptions (code, chat_id, kind, plugin_id, days, redeemed_by)
            VALUES (@Code, @chatId, @kind, @PluginId, @Days, @telegramUserId)
            ON CONFLICT (code) DO NOTHING
            """,
            new { grant.Code, chatId, kind = grant.IsPlugin ? "plugin" : "pro", grant.PluginId, grant.Days, telegramUserId },
            tx, cancellationToken: ct));

        DateTime? until;
        if (inserted == 0)
        {
            until = await CurrentUntilAsync(connection, tx, chatId, grant, ct);
            await tx.CommitAsync(ct);
            return new Result(false, until);
        }

        until = grant.IsPlugin
            ? await connection.ExecuteScalarAsync<DateTime?>(new CommandDefinition(
                """
                INSERT INTO group_plugins (chat_id, plugin_id, licensed_until)
                VALUES (@chatId, @PluginId, now() + make_interval(days => @Days))
                ON CONFLICT (chat_id, plugin_id) DO UPDATE
                   SET licensed_until = GREATEST(COALESCE(group_plugins.licensed_until, now()), now()) + make_interval(days => @Days),
                       updated_at = now()
                RETURNING licensed_until
                """,
                new { chatId, grant.PluginId, grant.Days }, tx, cancellationToken: ct))
            : await connection.ExecuteScalarAsync<DateTime?>(new CommandDefinition(
                """
                UPDATE groups
                   SET pro_until = GREATEST(COALESCE(pro_until, now()), now()) + make_interval(days => @Days),
                       updated_at = now()
                 WHERE chat_id = @chatId
                RETURNING pro_until
                """,
                new { chatId, grant.Days }, tx, cancellationToken: ct));

        await tx.CommitAsync(ct);
        states.Forget(chatId);
        return new Result(true, until);
    }

    private static Task<DateTime?> CurrentUntilAsync(NpgsqlConnection connection, NpgsqlTransaction tx, long chatId, LicenseGrant grant, CancellationToken ct) =>
        grant.IsPlugin
            ? connection.ExecuteScalarAsync<DateTime?>(new CommandDefinition(
                "SELECT licensed_until FROM group_plugins WHERE chat_id = @chatId AND plugin_id = @PluginId",
                new { chatId, grant.PluginId }, tx, cancellationToken: ct))
            : connection.ExecuteScalarAsync<DateTime?>(new CommandDefinition(
                "SELECT pro_until FROM groups WHERE chat_id = @chatId", new { chatId }, tx, cancellationToken: ct));
}
