using System.Text.Json;
using Dapper;
using GeekGuard.Core.Messaging;
using Npgsql;
using Telegram.Bot.Types;

namespace GeekGuard.Core.Groups;

/// <summary>A group that is closed right now.</summary>
/// <remarks>Dapper fills records through the constructor, so queries alias columns to these names, in this order.</remarks>
public sealed record GroupClosure(long ChatId, string Source, DateTime ClosedAt, DateTime? ReopenAt, string? SavedPermissions, int? NoticeMessageId);

/// <summary>
/// Closes and opens groups: while closed, only admins can post (Telegram's own member permissions do the work, so
/// nothing members send even reaches the chat). The permissions from before are saved and restored exactly, and a
/// notice explains the silence for as long as it lasts. Used by the manual /close and by scheduled closing plugins.
/// </summary>
public sealed class GroupCloser(NpgsqlDataSource db, BotActions actions, GroupDirectory groups, TimeProvider clock, ILogger<GroupCloser> log)
{
    /// <summary>Source of a closure made by an admin's command or the panel.</summary>
    public const string Manual = "manual";

    private static readonly Localized ClosedNotice = new(
        "🔒 <b>گروه بسته است.</b>\nفعلاً فقط ادمین‌ها می‌توانند پیام بدهند.",
        "🔒 <b>The group is closed.</b>\nFor now only admins can post.");
    private static readonly Localized ReopensAt = new("⏰ باز می‌شود: {0}", "⏰ Opens again: {0}");
    private static readonly Localized OpenedNotice = new("🔓 گروه باز شد. همه می‌توانند دوباره پیام بدهند.", "🔓 The group is open again. Everyone can post.");

    private const string Columns =
        "chat_id AS ChatId, source AS Source, closed_at AS ClosedAt, reopen_at AS ReopenAt, saved_permissions::text AS SavedPermissions, notice_message_id AS NoticeMessageId";

    public async Task<GroupClosure?> GetAsync(long chatId, CancellationToken ct = default)
    {
        await using var connection = await db.OpenConnectionAsync(ct);
        return await connection.QuerySingleOrDefaultAsync<GroupClosure>(new CommandDefinition(
            $"SELECT {Columns} FROM group_closures WHERE chat_id = @chatId", new { chatId }, cancellationToken: ct));
    }

    /// <summary>
    /// Closes the group (or, if it is already closed, changes when it opens and refreshes the notice).
    /// </summary>
    /// <param name="source"><see cref="Manual"/>, or the id of the plugin closing it.</param>
    /// <param name="reopenAt">When to open again by itself; null keeps it closed until someone opens it.</param>
    /// <param name="notice">Custom notice text (HTML, already localized); null for the standard one.</param>
    public async Task<ActionResult> CloseAsync(long chatId, string lang, string source, DateTimeOffset? reopenAt,
        string? notice = null, CancellationToken ct = default)
    {
        var existing = await GetAsync(chatId, ct);
        string? saved = existing?.SavedPermissions;

        if (existing is null)
        {
            var current = await actions.GetPermissionsAsync(chatId, ct);
            saved = current is null ? null : JsonSerializer.Serialize(current);

            var result = await actions.SetPermissionsAsync(chatId, Silenced(current), ct);
            if (!result.Succeeded) return result;
        }
        else if (existing.NoticeMessageId is { } oldNotice)
        {
            await actions.DeleteAsync(chatId, oldNotice, ct);
        }

        var text = notice ?? ClosedNotice.Get(lang);
        if (reopenAt is { } at) text += "\n" + ReopensAt.Format(lang, LocalTime.When(at, lang, clock));
        var sent = await actions.SendAsync(chatId, text, ct: ct, keep: true);

        await using var connection = await db.OpenConnectionAsync(ct);
        await connection.ExecuteAsync(new CommandDefinition(
            """
            INSERT INTO group_closures (chat_id, source, reopen_at, saved_permissions, notice_message_id)
            VALUES (@chatId, @source, @reopenAt, @saved::jsonb, @noticeId)
            ON CONFLICT (chat_id) DO UPDATE
               SET source = EXCLUDED.source, reopen_at = EXCLUDED.reopen_at, notice_message_id = EXCLUDED.notice_message_id
            """,
            new { chatId, source, reopenAt = reopenAt?.UtcDateTime, saved, noticeId = sent?.MessageId }, cancellationToken: ct));
        return ActionResult.Ok;
    }

    /// <summary>
    /// Opens the group, restoring the permissions it had before. Returns null if it was not closed.
    /// </summary>
    public async Task<ActionResult?> OpenAsync(long chatId, string lang, CancellationToken ct = default)
    {
        var closure = await GetAsync(chatId, ct);
        if (closure is null) return null;

        var saved = closure.SavedPermissions is { } json ? JsonSerializer.Deserialize<ChatPermissions>(json) : null;
        var result = await actions.SetPermissionsAsync(chatId, saved ?? Defaults(), ct);
        if (!result.Succeeded) return result;

        await using (var connection = await db.OpenConnectionAsync(ct))
        {
            await connection.ExecuteAsync(new CommandDefinition(
                "DELETE FROM group_closures WHERE chat_id = @chatId", new { chatId }, cancellationToken: ct));
        }

        if (closure.NoticeMessageId is { } notice) await actions.DeleteAsync(chatId, notice, ct);
        await actions.SendTemporaryAsync(chatId, OpenedNotice.Get(lang), TimeSpan.FromMinutes(2), ct: ct);
        return result;
    }

    /// <summary>Opens every group whose closing time is over. Called regularly by <see cref="GroupReopenService"/>.</summary>
    public async Task ReopenDueAsync(CancellationToken ct = default)
    {
        List<GroupClosure> due;
        await using (var connection = await db.OpenConnectionAsync(ct))
        {
            due = (await connection.QueryAsync<GroupClosure>(new CommandDefinition(
                $"SELECT {Columns} FROM group_closures WHERE reopen_at <= @now",
                new { now = clock.GetUtcNow().UtcDateTime }, cancellationToken: ct))).ToList();
        }

        foreach (var closure in due)
        {
            var lang = (await groups.GetAsync(closure.ChatId, ct))?.Lang ?? Languages.Persian;
            var result = await OpenAsync(closure.ChatId, lang, ct);
            if (result is { Succeeded: false } failed)
                log.LogWarning("Could not reopen {Chat}: {Problem} {Detail}", closure.ChatId, failed.Problem, failed.Detail);
        }
    }

    /// <summary>The group's own permissions with everything about sending switched off.</summary>
    private static ChatPermissions Silenced(ChatPermissions? current) => new()
    {
        CanSendMessages = false, CanSendAudios = false, CanSendDocuments = false, CanSendPhotos = false,
        CanSendVideos = false, CanSendVideoNotes = false, CanSendVoiceNotes = false, CanSendPolls = false,
        CanSendOtherMessages = false, CanAddWebPagePreviews = false,
        CanChangeInfo = current?.CanChangeInfo ?? false, CanInviteUsers = current?.CanInviteUsers ?? true,
        CanPinMessages = current?.CanPinMessages ?? false, CanManageTopics = current?.CanManageTopics ?? false,
    };

    /// <summary>Used only if the permissions from before could not be read: members may send anything.</summary>
    private static ChatPermissions Defaults() => new()
    {
        CanSendMessages = true, CanSendAudios = true, CanSendDocuments = true, CanSendPhotos = true,
        CanSendVideos = true, CanSendVideoNotes = true, CanSendVoiceNotes = true, CanSendPolls = true,
        CanSendOtherMessages = true, CanAddWebPagePreviews = true, CanInviteUsers = true,
    };
}

/// <summary>Opens groups whose closing time is over. Runs every 30 seconds; closures survive restarts.</summary>
public sealed class GroupReopenService(GroupCloser closer, ILogger<GroupReopenService> log) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(30));
        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            try
            {
                await closer.ReopenDueAsync(stoppingToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                log.LogWarning(ex, "Reopening closed groups failed; will retry");
            }
        }
    }
}
