using Dapper;
using GeekGuard.Core.Callbacks;
using GeekGuard.Core.Groups;
using GeekGuard.Core.Messaging;
using GeekGuard.Core.Owner;
using GeekGuard.Core.Panel;
using GeekGuard.Modules.Moderation;
using Npgsql;
using Telegram.Bot.Types;
using Telegram.Bot.Types.ReplyMarkups;

namespace GeekGuard.Modules.SpamNetwork;

/// <summary>
/// The anti-spam network: someone the bot itself caught spamming (links, banned words, flooding) in several groups
/// is stopped at the door of every other group that uses Geek Guard. Admin bans never count, so nobody can get an
/// innocent person blocked everywhere. Free for everyone: the more groups, the better it protects all of them.
/// </summary>
public sealed class SpamNetworkPlugin : IGeekGuardPlugin
{
    public const string Id = "spam-network";

    public PluginManifest Manifest { get; } = new(Id, "شبکه‌ی ضداسپم", "Anti-spam network", PluginTier.Free, EnabledByDefault: true);

    public void ConfigureServices(IServiceCollection services, IConfiguration configuration)
    {
        services.AddSingleton<SpamNetwork>();
        services.AddGroupMessageHandler<SpamNetworkJoinHandler>();
        services.AddCallbackHandler<SpamNetworkButtonHandler>();
        services.AddPanelSection<SpamNetworkSection>();
        services.AddSingleton<IOwnerCommand, UnflagCommand>();
        services.AddSingleton<IOwnerStats, SpamNetworkStats>();
        services.AddHelpTopic(Help.HelpTopics.SpamNetwork);
        services.AddPluginMigrations(Id, GetType());
    }
}

public enum SpamNetworkAction
{
    Off = 0,
    Mute = 1,
    Ban = 2,
}

public sealed class SpamNetworkSettings
{
    /// <summary>What happens when a known spammer joins.</summary>
    public SpamNetworkAction Action { get; set; } = SpamNetworkAction.Mute;
}

/// <summary>Records what the bot caught and answers "is this person a known spammer?".</summary>
public sealed class SpamNetwork(NpgsqlDataSource db, TimeProvider clock)
{
    /// <summary>Caught in at least this many different groups…</summary>
    public const int GroupsNeeded = 3;

    /// <summary>…within this many days. Older events are forgotten.</summary>
    public const int Days = 30;

    /// <summary>Kinds of catches that count. Locks do not: a photo in a no-photo group breaks a house rule, it is not spam.</summary>
    public static readonly IReadOnlySet<string> CountedKinds = new HashSet<string> { "anti-link", "word-filter", "flood" };

    private DateTimeOffset _lastCleanup = DateTimeOffset.MinValue;

    /// <summary>Notes that the bot caught <paramref name="userId"/> in a group. Kinds outside <see cref="CountedKinds"/> are ignored.</summary>
    public async Task RecordAsync(long chatId, long userId, string kind, CancellationToken ct = default)
    {
        if (!CountedKinds.Contains(kind)) return;

        await using var connection = await db.OpenConnectionAsync(ct);
        await connection.ExecuteAsync(new CommandDefinition(
            "INSERT INTO spam_events (user_id, chat_id, kind) VALUES (@userId, @chatId, @kind)",
            new { userId, chatId, kind }, cancellationToken: ct));

        // Forget old events now and then, so the table stays small and old mistakes fade.
        var now = clock.GetUtcNow();
        if (now - _lastCleanup > TimeSpan.FromHours(1))
        {
            _lastCleanup = now;
            await connection.ExecuteAsync(new CommandDefinition(
                $"DELETE FROM spam_events WHERE at < now() - interval '{Days} days'", cancellationToken: ct));
        }
    }

    /// <summary>Whether the person was caught in enough different groups recently.</summary>
    public async Task<bool> IsKnownSpammerAsync(long userId, CancellationToken ct = default)
    {
        await using var connection = await db.OpenConnectionAsync(ct);
        var groups = await connection.ExecuteScalarAsync<int>(new CommandDefinition(
            $"SELECT count(DISTINCT chat_id) FROM spam_events WHERE user_id = @userId AND at > now() - interval '{Days} days'",
            new { userId }, cancellationToken: ct));
        return groups >= GroupsNeeded;
    }

    public async Task<bool> IsTrustedAsync(long chatId, long userId, CancellationToken ct = default)
    {
        await using var connection = await db.OpenConnectionAsync(ct);
        return await connection.ExecuteScalarAsync<bool>(new CommandDefinition(
            "SELECT EXISTS (SELECT 1 FROM spam_network_trusted WHERE chat_id = @chatId AND user_id = @userId)",
            new { chatId, userId }, cancellationToken: ct));
    }

    /// <summary>An admin let this person in: the network leaves them alone in this group from now on.</summary>
    public async Task TrustAsync(long chatId, long userId, CancellationToken ct = default)
    {
        await using var connection = await db.OpenConnectionAsync(ct);
        await connection.ExecuteAsync(new CommandDefinition(
            "INSERT INTO spam_network_trusted (chat_id, user_id) VALUES (@chatId, @userId) ON CONFLICT DO NOTHING",
            new { chatId, userId }, cancellationToken: ct));
    }

    public async Task RecordCatchAsync(long chatId, long userId, CancellationToken ct = default)
    {
        await using var connection = await db.OpenConnectionAsync(ct);
        await connection.ExecuteAsync(new CommandDefinition(
            "INSERT INTO spam_network_catches (chat_id, user_id) VALUES (@chatId, @userId)",
            new { chatId, userId }, cancellationToken: ct));
    }

    public async Task<int> CatchesInAsync(long chatId, CancellationToken ct = default)
    {
        await using var connection = await db.OpenConnectionAsync(ct);
        return await connection.ExecuteScalarAsync<int>(new CommandDefinition(
            "SELECT count(*) FROM spam_network_catches WHERE chat_id = @chatId", new { chatId }, cancellationToken: ct));
    }

    /// <summary>Removes someone from the network entirely (the owner's /unflag). Returns how many records went.</summary>
    public async Task<int> ForgetAsync(long userId, CancellationToken ct = default)
    {
        await using var connection = await db.OpenConnectionAsync(ct);
        return await connection.ExecuteAsync(new CommandDefinition(
            "DELETE FROM spam_events WHERE user_id = @userId", new { userId }, cancellationToken: ct));
    }

    public async Task<(int KnownSpammers, int Catches)> TotalsAsync(CancellationToken ct = default)
    {
        await using var connection = await db.OpenConnectionAsync(ct);
        return await connection.QuerySingleAsync<(int, int)>(new CommandDefinition(
            $"""
            SELECT (SELECT count(*) FROM (SELECT user_id FROM spam_events WHERE at > now() - interval '{Days} days'
                                          GROUP BY user_id HAVING count(DISTINCT chat_id) >= {GroupsNeeded}) s)::int,
                   (SELECT count(*) FROM spam_network_catches)::int
            """, cancellationToken: ct));
    }
}

/// <summary>Stops known spammers when they join: muted (or banned), with a one-tap "let them in" for admins.</summary>
public sealed class SpamNetworkJoinHandler(
    SpamNetwork network,
    PluginStateStore states,
    GroupAdmins admins,
    BotActions actions,
    BotIdentity me) : IGroupMessageHandler
{
    private static readonly Localized Held = new(
        "🛡 {0} در شبکه‌ی Geek Guard به‌عنوان اسپمر شناخته شده و {1}.\nادمین‌ها اگر می‌شناسندش، می‌توانند آزادش کنند.",
        "🛡 {0} is a known spammer in the Geek Guard network and was {1}.\nAdmins who know them can let them in.");
    private static readonly Localized MutedWord = new("ساکت شد", "muted");
    private static readonly Localized BannedWord = new("بن شد", "banned");
    private static readonly Localized LetIn = new("✅ آزادش کن", "✅ Let them in");

    /// <summary>The notice stays long enough for an admin to see it.</summary>
    private static readonly TimeSpan NoticeLifetime = TimeSpan.FromMinutes(10);

    public string PluginId => SpamNetworkPlugin.Id;

    // After the join locks, before the welcome (which skips whoever is held here).
    public int Order => HandlerOrder.Membership + 5;

    public async Task<HandlerResult> HandleAsync(GroupMessageContext context, CancellationToken ct)
    {
        var message = context.Message;
        if (message.NewChatMembers is not { Length: > 0 } joined) return HandlerResult.Continue;

        var chatId = context.ChatId;
        var settings = await states.GetSettingsAsync<SpamNetworkSettings>(chatId, SpamNetworkPlugin.Id, ct);
        if (settings.Action == SpamNetworkAction.Off) return HandlerResult.Continue;

        // Someone an admin added on purpose is the admin's call.
        if (message.From is { } adder && joined.All(u => u.Id != adder.Id) && await admins.IsAdminAsync(chatId, adder.Id, ct))
            return HandlerResult.Continue;

        var lang = context.Group.Lang;
        foreach (var user in joined.Where(u => !u.IsBot && u.Id != me.Id))
        {
            if (!await network.IsKnownSpammerAsync(user.Id, ct) || await network.IsTrustedAsync(chatId, user.Id, ct)) continue;

            var result = settings.Action == SpamNetworkAction.Ban
                ? await actions.BanAsync(chatId, user.Id, null, ct)
                : await actions.MuteAsync(chatId, user.Id, null, ct);
            if (!result.Succeeded) continue;

            context.HeldNewcomers.Add(user.Id);
            await network.RecordCatchAsync(chatId, user.Id, ct);

            var what = (settings.Action == SpamNetworkAction.Ban ? BannedWord : MutedWord).Raw(lang);
            var keyboard = new InlineKeyboardMarkup(InlineKeyboardButton.WithCallbackData(LetIn.Get(lang),
                $"{SpamNetworkButtonHandler.Prefix}:free:{user.Id}"));
            var notice = await actions.SendAsync(chatId, Held.Format(lang, Html.MentionWithId(user), what), keyboard: keyboard, ct: ct);
            if (notice is not null) actions.ScheduleDelete(chatId, notice.MessageId, NoticeLifetime);
        }
        return HandlerResult.Continue;
    }
}

/// <summary>"✅ Let them in": an admin with the restrict right frees the person and trusts them in this group.</summary>
public sealed class SpamNetworkButtonHandler(SpamNetwork network, GroupAdmins admins, BotActions actions, GroupDirectory groups)
    : ICallbackHandler
{
    public const string Prefix = "sn";

    private static readonly Localized NotAllowed = new(
        "⛔ فقط ادمین‌هایی که اجازه‌ی محدود کردن اعضا دارند.", "⛔ Only admins who can restrict members.");
    private static readonly Localized Freed = new("✅ {0} توسط {1} آزاد شد.", "✅ {0} was let in by {1}.");

    string ICallbackHandler.Prefix => Prefix;

    public async Task HandleAsync(CallbackQuery query, string[] args, CancellationToken ct)
    {
        if (query.Message is not { Chat.Id: < 0 } notice || args is not ["free", var idText] || !long.TryParse(idText, out var userId))
        {
            await actions.AnswerButtonAsync(query.Id, ct: ct);
            return;
        }

        var chatId = notice.Chat.Id;
        var lang = (await groups.GetAsync(chatId, ct))?.Lang ?? Languages.Persian;
        if (!await admins.CanRestrictAsync(chatId, query.From.Id, ct))
        {
            await actions.AnswerButtonAsync(query.Id, NotAllowed.Get(lang), alert: true, ct);
            return;
        }

        // Undo whichever was done; both are harmless if they were not.
        await actions.UnmuteAsync(chatId, userId, ct);
        await actions.UnbanAsync(chatId, userId, ct);
        await network.TrustAsync(chatId, userId, ct);

        await actions.AnswerButtonAsync(query.Id, ct: ct);
        await actions.EditAsync(chatId, notice.MessageId,
            Freed.Format(lang, $"<code>{userId}</code>", Html.Escape(Html.DisplayName(query.From))), null, ct);
        actions.ScheduleDelete(chatId, notice.MessageId, TimeSpan.FromMinutes(1));
    }
}

/// <summary>Panel: what happens to known spammers who join.</summary>
public sealed class SpamNetworkSection(PluginStateStore states, SpamNetwork network) : IPanelSection
{
    private static readonly Localized Text = new(
        "🛡 <b>شبکه‌ی ضداسپم</b>\n\nکسی که ربات در {0} گروه مختلف به‌خاطر لینک، کلمه‌ی فیلترشده یا فلود گرفته باشد، در همه‌ی گروه‌های Geek Guard اسپمر شناخته می‌شود. وقتی وارد این گروه شود، همان لحظه جلویش گرفته می‌شود و ادمین‌ها می‌توانند با یک کلیک آزادش کنند.\n\nبن دستی ادمین‌ها حساب نمی‌شود و اعضای فعلی گروه دست نمی‌خورند.\n\nتا حالا در این گروه: <b>{1}</b> اسپمر",
        "🛡 <b>Anti-spam network</b>\n\nSomeone the bot caught for links, banned words or flooding in {0} different groups is a known spammer in every Geek Guard group. When they join this group they are stopped right away, and admins can let them in with one tap.\n\nAdmin bans never count, and current members are never touched.\n\nStopped here so far: <b>{1}</b>");

    public string Id => "network";

    public string PluginId => SpamNetworkPlugin.Id;

    public int Order => 35;

    public Localized Title { get; } = new("🛡 شبکه‌ی ضداسپم", "🛡 Spam network");

    public bool HasPluginSwitch => false;

    public async Task<PanelView> ShowAsync(PanelContext context, CancellationToken ct)
    {
        var s = await states.GetSettingsAsync<SpamNetworkSettings>(context.ChatId, SpamNetworkPlugin.Id, ct);
        var fa = context.Lang != Languages.English;
        var caught = await network.CatchesInAsync(context.ChatId, ct);

        var rows = new List<InlineKeyboardButton[]>
        {
            PanelButtons.Header(fa ? "🚪 وقتی اسپمر شناخته‌شده وارد شود" : "🚪 When a known spammer joins"),
            new[]
            {
                PanelButtons.Choice(fa ? "خاموش" : "Off", s.Action == SpamNetworkAction.Off, context, Id, "act", "Off"),
                PanelButtons.Choice(fa ? "🔇 سکوت" : "🔇 Mute", s.Action == SpamNetworkAction.Mute, context, Id, "act", "Mute"),
                PanelButtons.Choice(fa ? "⛔ بن" : "⛔ Ban", s.Action == SpamNetworkAction.Ban, context, Id, "act", "Ban"),
            },
            new[] { PanelButtons.BackToGroup(context) },
        };

        var html = context.T(Text).Replace("{0}", SpamNetwork.GroupsNeeded.ToString()).Replace("{1}", caught.ToString());
        return new PanelView(html, new InlineKeyboardMarkup(rows));
    }

    public async Task<string?> HandleAsync(PanelContext context, string[] args, CancellationToken ct)
    {
        if (args is ["act", var value] && Enum.TryParse<SpamNetworkAction>(value, out var action) && Enum.IsDefined(action))
            await states.SaveSettingsAsync(context.ChatId, SpamNetworkPlugin.Id, new SpamNetworkSettings { Action = action }, ct);
        return null;
    }
}

/// <summary>/unflag &lt;user id&gt;: takes someone out of the network (a mistake, or they changed their ways).</summary>
public sealed class UnflagCommand(SpamNetwork network) : IOwnerCommand
{
    public string Name => "unflag";

    public async Task<string> RunAsync(string[] args, CancellationToken ct)
    {
        if (args is not [var idText] || !long.TryParse(idText, out var userId))
            return "🛡 <code>/unflag &lt;آیدی عددی کاربر&gt;</code>";

        var removed = await network.ForgetAsync(userId, ct);
        return removed == 0
            ? $"ℹ️ <code>{userId}</code> در شبکه سابقه‌ای نداشت."
            : $"✅ <code>{userId}</code> از شبکه‌ی ضداسپم پاک شد ({removed} مورد).";
    }
}

public sealed class SpamNetworkStats(SpamNetwork network) : IOwnerStats
{
    public async Task<IEnumerable<string>> LinesAsync(CancellationToken ct)
    {
        var (known, catches) = await network.TotalsAsync(ct);
        return [$"🛡 اسپمرهای شناخته‌شده‌ی شبکه: <b>{known}</b>", $"🚪 دفعاتی که شبکه جلوی اسپمر را گرفت: <b>{catches}</b>"];
    }
}
