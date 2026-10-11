using System.Collections.Concurrent;
using System.Text.Json;
using Dapper;
using Npgsql;

namespace GeekGuard.Core.Plugins;

/// <summary>A group's choices about one plugin. Null <see cref="Enabled"/> means "use the plugin's default".</summary>
public sealed record PluginState(string PluginId, bool? Enabled, DateTime? LicensedUntil);

/// <summary>
/// Decides whether a plugin runs in a group: it must be switched on (or on by default),
/// and Pro plugins also need an unexpired licence: Pro for the whole group, or for that plugin alone.
/// Per-group rows are cached in memory.
/// </summary>
public sealed class PluginStateStore(NpgsqlDataSource db, TimeProvider clock)
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    private readonly ConcurrentDictionary<long, IReadOnlyDictionary<string, PluginState>> _cache = new();
    private readonly ConcurrentDictionary<(long Chat, string Plugin), object> _settings = new();
    private readonly ConcurrentDictionary<long, DateTime?> _proUntil = new();

    public async Task<bool> IsActiveAsync(long chatId, PluginManifest plugin, CancellationToken ct = default)
    {
        var states = await GetStatesAsync(chatId, ct);
        states.TryGetValue(plugin.Id, out var state);
        var proUntil = plugin.Tier == PluginTier.Pro ? await GetProUntilAsync(chatId, ct) : null;
        return IsActive(plugin, state, clock.GetUtcNow().UtcDateTime, proUntil);
    }

    /// <summary>The activation rule on its own, so it can be tested without a database.</summary>
    public static bool IsActive(PluginManifest plugin, PluginState? state, DateTime utcNow, DateTime? groupProUntil = null)
    {
        var enabled = state?.Enabled ?? plugin.EnabledByDefault;
        var licensed = plugin.Tier == PluginTier.Free || state?.LicensedUntil > utcNow || groupProUntil > utcNow;
        return enabled && licensed;
    }

    /// <summary>Until when the group has Pro (null: never had it). It may be in the past.</summary>
    public async Task<DateTime?> GetProUntilAsync(long chatId, CancellationToken ct = default)
    {
        if (_proUntil.TryGetValue(chatId, out var cached)) return cached;

        await using var connection = await db.OpenConnectionAsync(ct);
        var until = await connection.ExecuteScalarAsync<DateTime?>(new CommandDefinition(
            "SELECT pro_until FROM groups WHERE chat_id = @chatId", new { chatId }, cancellationToken: ct));
        _proUntil[chatId] = until;
        return until;
    }

    /// <summary>Whether the group has Pro right now.</summary>
    public async Task<bool> IsProAsync(long chatId, CancellationToken ct = default) =>
        await GetProUntilAsync(chatId, ct) > clock.GetUtcNow().UtcDateTime;

    /// <summary>Gives the group Pro until <paramref name="until"/>, or takes it away (null).</summary>
    public async Task SetProUntilAsync(long chatId, DateTime? until, CancellationToken ct = default)
    {
        await using var connection = await db.OpenConnectionAsync(ct);
        await connection.ExecuteAsync(new CommandDefinition(
            "UPDATE groups SET pro_until = @until, updated_at = now() WHERE chat_id = @chatId",
            new { chatId, until }, cancellationToken: ct));
        _proUntil[chatId] = until;
    }

    public async Task<IReadOnlyDictionary<string, PluginState>> GetStatesAsync(long chatId, CancellationToken ct = default)
    {
        if (_cache.TryGetValue(chatId, out var cached)) return cached;

        await using var connection = await db.OpenConnectionAsync(ct);
        var rows = await connection.QueryAsync<PluginState>(new CommandDefinition(
            """
            SELECT plugin_id AS PluginId, enabled AS Enabled, licensed_until AS LicensedUntil
              FROM group_plugins
             WHERE chat_id = @chatId
            """,
            new { chatId }, cancellationToken: ct));

        var states = rows.ToDictionary(r => r.PluginId, StringComparer.Ordinal);
        _cache[chatId] = states;
        return states;
    }

    /// <summary>Switches a plugin on or off for a group (the settings panel will call this).</summary>
    public async Task SetEnabledAsync(long chatId, string pluginId, bool enabled, CancellationToken ct = default)
    {
        await using var connection = await db.OpenConnectionAsync(ct);
        await connection.ExecuteAsync(new CommandDefinition(
            """
            INSERT INTO group_plugins (chat_id, plugin_id, enabled) VALUES (@chatId, @pluginId, @enabled)
            ON CONFLICT (chat_id, plugin_id) DO UPDATE SET enabled = EXCLUDED.enabled, updated_at = now()
            """,
            new { chatId, pluginId, enabled }, cancellationToken: ct));
        _cache.TryRemove(chatId, out _);
    }

    /// <summary>
    /// A plugin's settings for a group, stored as JSON. Missing settings (or fields added in a later version)
    /// get the defaults of <typeparamref name="T"/>. Callers must treat the result as read-only and save a copy.
    /// </summary>
    public async Task<T> GetSettingsAsync<T>(long chatId, string pluginId, CancellationToken ct = default) where T : class, new()
    {
        if (_settings.TryGetValue((chatId, pluginId), out var cached) && cached is T typed) return typed;

        await using var connection = await db.OpenConnectionAsync(ct);
        var json = await connection.ExecuteScalarAsync<string?>(new CommandDefinition(
            "SELECT settings::text FROM group_plugins WHERE chat_id = @chatId AND plugin_id = @pluginId",
            new { chatId, pluginId }, cancellationToken: ct));

        T settings;
        try
        {
            settings = json is null ? new T() : JsonSerializer.Deserialize<T>(json, Json) ?? new T();
        }
        catch (JsonException)
        {
            settings = new T(); // unreadable settings must not take a group's protection down
        }

        _settings[(chatId, pluginId)] = settings;
        return settings;
    }

    public async Task SaveSettingsAsync<T>(long chatId, string pluginId, T settings, CancellationToken ct = default) where T : class
    {
        var json = JsonSerializer.Serialize(settings, Json);
        await using var connection = await db.OpenConnectionAsync(ct);
        await connection.ExecuteAsync(new CommandDefinition(
            """
            INSERT INTO group_plugins (chat_id, plugin_id, settings) VALUES (@chatId, @pluginId, @json::jsonb)
            ON CONFLICT (chat_id, plugin_id) DO UPDATE SET settings = EXCLUDED.settings, updated_at = now()
            """,
            new { chatId, pluginId, json }, cancellationToken: ct));
        _settings[(chatId, pluginId)] = settings;
    }
}
