using System.Collections.Concurrent;
using Dapper;
using Npgsql;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;

namespace GeekGuard.Core.Groups;

/// <summary>
/// Answers "is this person an admin here?" quickly enough to ask on every message, and keeps the
/// group_admins table (used by the settings panel) in sync with Telegram.
/// </summary>
/// <remarks>
/// Two levels of trust: <see cref="IsAdminAsync"/> uses a list cached for 10 minutes and decides who filters
/// skip. <see cref="CanRestrictAsync"/> checks live (2-minute cache) whether someone may mute and ban,
/// because that guards actions against other people.
/// </remarks>
public sealed class GroupAdmins(ITelegramBotClient bot, NpgsqlDataSource db, ILogger<GroupAdmins> log)
{
    /// <summary>Telegram's stand-in user for admins posting anonymously ("@GroupAnonymousBot").</summary>
    public const long AnonymousAdminId = 1087968824;

    private static readonly TimeSpan AdminListTtl = TimeSpan.FromMinutes(10);
    private static readonly TimeSpan RightsTtl = TimeSpan.FromMinutes(2);
    private static readonly TimeSpan RetryAfterFailure = TimeSpan.FromMinutes(1);

    private readonly ConcurrentDictionary<long, (HashSet<long> Ids, DateTime Expires)> _admins = new();
    private readonly ConcurrentDictionary<(long Chat, long User), (bool CanRestrict, DateTime Expires)> _rights = new();

    public async Task<bool> IsAdminAsync(long chatId, long userId, CancellationToken ct = default) =>
        userId == AnonymousAdminId || (await GetAdminIdsAsync(chatId, ct)).Contains(userId);

    /// <summary>True for the group owner, and for admins allowed to restrict members.</summary>
    public async Task<bool> CanRestrictAsync(long chatId, long userId, CancellationToken ct = default)
    {
        if (_rights.TryGetValue((chatId, userId), out var hit) && hit.Expires > DateTime.UtcNow) return hit.CanRestrict;

        bool canRestrict;
        try
        {
            var member = await bot.GetChatMember(chatId, userId, ct);
            canRestrict = member is ChatMemberOwner or ChatMemberAdministrator { CanRestrictMembers: true };
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            log.LogDebug("getChatMember {Chat}/{User} failed: {Message}", chatId, userId, ex.Message);
            canRestrict = false;
        }

        _rights[(chatId, userId)] = (canRestrict, DateTime.UtcNow + RightsTtl);
        return canRestrict;
    }

    public async Task<IReadOnlySet<long>> GetAdminIdsAsync(long chatId, CancellationToken ct = default)
    {
        if (_admins.TryGetValue(chatId, out var hit) && hit.Expires > DateTime.UtcNow) return hit.Ids;
        return await RefreshAsync(chatId, ct);
    }

    /// <summary>Reloads the admin list from Telegram and stores it.</summary>
    public async Task<IReadOnlySet<long>> RefreshAsync(long chatId, CancellationToken ct = default)
    {
        try
        {
            var admins = await bot.GetChatAdministrators(chatId, cancellationToken: ct);
            var ids = admins.Where(a => !a.User.IsBot).Select(a => a.User.Id).ToHashSet();
            await SaveAsync(chatId, ids, ct);

            _admins[chatId] = (ids, DateTime.UtcNow + AdminListTtl);
            ForgetRights(chatId);
            return ids;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            log.LogWarning("Could not load admins of {Chat}: {Message}", chatId, ex.Message);
            // Keep a stale list rather than treating every admin as a member; retry in a minute, not on every message.
            var ids = _admins.TryGetValue(chatId, out var stale) ? stale.Ids : [];
            _admins[chatId] = (ids, DateTime.UtcNow + RetryAfterFailure);
            return ids;
        }
    }

    /// <summary>Someone's status changed in a group: drop what we cached about it.</summary>
    public void OnMemberChanged(ChatMemberUpdated change)
    {
        if (IsAdminStatus(change.OldChatMember.Status) != IsAdminStatus(change.NewChatMember.Status)
            || change.NewChatMember is ChatMemberAdministrator)
        {
            _admins.TryRemove(change.Chat.Id, out _);
            _rights.TryRemove((change.Chat.Id, change.NewChatMember.User.Id), out _);
        }
    }

    /// <summary>The bot's own rights in a group, for diagnostics and setup hints.</summary>
    public async Task<(bool CanDelete, bool CanRestrict)> GetBotRightsAsync(long chatId, long botId, CancellationToken ct = default)
    {
        try
        {
            var me = await bot.GetChatMember(chatId, botId, ct);
            return me is ChatMemberAdministrator a ? (a.CanDeleteMessages, a.CanRestrictMembers) : (false, false);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            log.LogDebug("Could not read own rights in {Chat}: {Message}", chatId, ex.Message);
            return (false, false);
        }
    }

    public static bool IsAdminStatus(ChatMemberStatus status) =>
        status is ChatMemberStatus.Administrator or ChatMemberStatus.Creator;

    private void ForgetRights(long chatId)
    {
        foreach (var key in _rights.Keys.Where(k => k.Chat == chatId)) _rights.TryRemove(key, out _);
    }

    private async Task SaveAsync(long chatId, IReadOnlyCollection<long> userIds, CancellationToken ct)
    {
        await using var connection = await db.OpenConnectionAsync(ct);
        await using var transaction = await connection.BeginTransactionAsync(ct);
        await connection.ExecuteAsync(new CommandDefinition(
            "DELETE FROM group_admins WHERE chat_id = @chatId", new { chatId }, transaction, cancellationToken: ct));
        await connection.ExecuteAsync(new CommandDefinition(
            "INSERT INTO group_admins (chat_id, user_id) SELECT @chatId, unnest(@userIds)",
            new { chatId, userIds = userIds.ToArray() }, transaction, cancellationToken: ct));
        await transaction.CommitAsync(ct);
    }
}
