using Dapper;
using Npgsql;

namespace GeekGuard.Modules.Reports;

/// <summary>What admins can do from a report's buttons.</summary>
public enum ReportAction
{
    Delete,
    Mute,
    Ban,
    Dismiss,
}

/// <remarks>Dapper fills records through the constructor, so queries alias columns to these names, in this order.</remarks>
public sealed record Report(
    long Id, long ChatId, int MessageId, long? AuthorId, long ReporterId, string Lang,
    long? HandledBy, string? HandledByName, string? HandledAction);

public sealed class ReportStore(NpgsqlDataSource db)
{
    private const string Columns =
        """
        id AS Id, chat_id AS ChatId, message_id AS MessageId, author_id AS AuthorId, reporter_id AS ReporterId,
        lang AS Lang, handled_by AS HandledBy, handled_by_name AS HandledByName, handled_action AS HandledAction
        """;

    public async Task<long> CreateAsync(long chatId, int messageId, long? authorId, long reporterId, string lang,
        CancellationToken ct = default)
    {
        await using var connection = await db.OpenConnectionAsync(ct);
        return await connection.ExecuteScalarAsync<long>(new CommandDefinition(
            """
            INSERT INTO reports (chat_id, message_id, author_id, reporter_id, lang)
            VALUES (@chatId, @messageId, @authorId, @reporterId, @lang)
            RETURNING id
            """,
            new { chatId, messageId, authorId, reporterId, lang }, cancellationToken: ct));
    }

    public async Task AddDeliveryAsync(long reportId, long adminId, int messageId, CancellationToken ct = default)
    {
        await using var connection = await db.OpenConnectionAsync(ct);
        await connection.ExecuteAsync(new CommandDefinition(
            "INSERT INTO report_deliveries (report_id, admin_id, message_id) VALUES (@reportId, @adminId, @messageId) ON CONFLICT DO NOTHING",
            new { reportId, adminId, messageId }, cancellationToken: ct));
    }

    public async Task<Report?> GetAsync(long id, CancellationToken ct = default)
    {
        await using var connection = await db.OpenConnectionAsync(ct);
        return await connection.QuerySingleOrDefaultAsync<Report>(new CommandDefinition(
            $"SELECT {Columns} FROM reports WHERE id = @id", new { id }, cancellationToken: ct));
    }

    /// <summary>
    /// Marks the report handled by this admin, unless another admin got there first (two admins pressing at the
    /// same moment). Returns the report when this admin won, null otherwise.
    /// </summary>
    public async Task<Report?> TryClaimAsync(long id, long adminId, string adminName, ReportAction action, CancellationToken ct = default)
    {
        await using var connection = await db.OpenConnectionAsync(ct);
        return await connection.QuerySingleOrDefaultAsync<Report>(new CommandDefinition(
            $"""
            UPDATE reports
               SET handled_by = @adminId, handled_by_name = @adminName, handled_action = @action, handled_at = now()
             WHERE id = @id AND handled_at IS NULL
            RETURNING {Columns}
            """,
            new { id, adminId, adminName, action = action.ToString().ToLowerInvariant() }, cancellationToken: ct));
    }

    /// <summary>Undoes a claim when Telegram refused the action, so another admin can try.</summary>
    public async Task ReleaseAsync(long id, CancellationToken ct = default)
    {
        await using var connection = await db.OpenConnectionAsync(ct);
        await connection.ExecuteAsync(new CommandDefinition(
            "UPDATE reports SET handled_by = NULL, handled_by_name = NULL, handled_action = NULL, handled_at = NULL WHERE id = @id",
            new { id }, cancellationToken: ct));
    }

    public async Task<IReadOnlyList<(long AdminId, int MessageId)>> GetDeliveriesAsync(long reportId, CancellationToken ct = default)
    {
        await using var connection = await db.OpenConnectionAsync(ct);
        var rows = await connection.QueryAsync<(long, int)>(new CommandDefinition(
            "SELECT admin_id, message_id FROM report_deliveries WHERE report_id = @reportId",
            new { reportId }, cancellationToken: ct));
        return rows.ToList();
    }
}
