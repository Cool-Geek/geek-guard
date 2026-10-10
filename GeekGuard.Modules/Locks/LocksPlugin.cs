using System.Text.Json.Serialization;
using CoolGeek.PersianText;
using GeekGuard.Core.Groups;
using GeekGuard.Core.Messaging;
using GeekGuard.Modules.Filters;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace GeekGuard.Modules.Locks;

/// <summary>
/// Content locks: photos, stickers, forwards, @usernames… Locked content from members is removed.
/// Also removes bots that members add, and can hide join/leave notices.
/// </summary>
public sealed class LocksPlugin : IGeekGuardPlugin
{
    public const string Id = "locks";

    public PluginManifest Manifest { get; } = new(Id, "قفل‌ها", "Locks", PluginTier.Free, EnabledByDefault: true);

    public void ConfigureServices(IServiceCollection services, IConfiguration configuration)
    {
        services.TryAddSingleton<ViolationService>();
        services.AddGroupMessageHandler<LockCommandsHandler>();
        services.AddGroupMessageHandler<JoinLocksHandler>();
        services.AddGroupMessageHandler<ContentLocksHandler>();
        services.AddPanelSection<LocksSection>();
        services.AddHelpTopic(Help.HelpTopics.Locks);
    }
}

/// <summary>Per-group locks.</summary>
public sealed class LocksSettings
{
    /// <summary>Locked kinds. Only "bots added by members" is locked out of the box.</summary>
    [JsonConverter(typeof(JsonStringEnumListConverter))]
    public List<LockType> Locked { get; set; } = [LockType.Bots];

    /// <summary>Warn members who post locked content (off: locks are house rules, not offences).</summary>
    public bool WarnOnViolation { get; set; }

    public bool IsLocked(LockType type) => Locked.Contains(type);
}

/// <summary>Stores lock names as words ("Photo") rather than numbers, so stored settings stay readable.</summary>
public sealed class JsonStringEnumListConverter : JsonConverter<List<LockType>>
{
    public override List<LockType> Read(ref System.Text.Json.Utf8JsonReader reader, Type typeToConvert, System.Text.Json.JsonSerializerOptions options)
    {
        var result = new List<LockType>();
        if (reader.TokenType != System.Text.Json.JsonTokenType.StartArray) return result;
        while (reader.Read() && reader.TokenType != System.Text.Json.JsonTokenType.EndArray)
        {
            // Unknown names (a lock removed in a later version) are skipped instead of breaking the group.
            if (reader.TokenType == System.Text.Json.JsonTokenType.String && Enum.TryParse<LockType>(reader.GetString(), true, out var type))
                result.Add(type);
        }
        return result;
    }

    public override void Write(System.Text.Json.Utf8JsonWriter writer, List<LockType> value, System.Text.Json.JsonSerializerOptions options)
    {
        writer.WriteStartArray();
        foreach (var type in value) writer.WriteStringValue(type.ToString());
        writer.WriteEndArray();
    }
}

/// <summary>Removes locked content posted by members.</summary>
public sealed class ContentLocksHandler(PluginStateStore states, ViolationService violations) : IGroupMessageHandler
{
    private static readonly Localized Reason = new("ارسال {0} در این گروه قفل است.", "{0} are locked in this group.");

    public string PluginId => LocksPlugin.Id;

    public int Order => HandlerOrder.ContentFilters;

    public async Task<HandlerResult> HandleAsync(GroupMessageContext context, CancellationToken ct)
    {
        if (context.SenderIsAdmin) return HandlerResult.Continue;

        var settings = await states.GetSettingsAsync<LocksSettings>(context.ChatId, LocksPlugin.Id, ct);
        var locked = LockTypes.Find(context.Message, context.NormalizedText, settings.Locked.ToHashSet());
        if (locked is not { } type) return HandlerResult.Continue;

        var lang = context.Group.Lang;
        var reason = new Localized(Reason.Format(Languages.Persian, LockTypes.Name(type, Languages.Persian)),
                                   Reason.Format(Languages.English, LockTypes.Name(type, Languages.English)));
        await violations.HandleAsync(context, reason, settings.WarnOnViolation, LocksPlugin.Id, ct);
        return HandlerResult.Stop;
    }
}

/// <summary>Bot lock and join/leave notices. Runs before content filters and lets the welcome message run after it.</summary>
public sealed class JoinLocksHandler(PluginStateStore states, BotActions actions, GroupAdmins admins, BotIdentity me) : IGroupMessageHandler
{
    public string PluginId => LocksPlugin.Id;

    public int Order => HandlerOrder.Membership;

    public async Task<HandlerResult> HandleAsync(GroupMessageContext context, CancellationToken ct)
    {
        var message = context.Message;
        var isJoin = message.NewChatMembers is { Length: > 0 };
        var isLeave = message.LeftChatMember is not null;
        if (!isJoin && !isLeave) return HandlerResult.Continue;

        var settings = await states.GetSettingsAsync<LocksSettings>(context.ChatId, LocksPlugin.Id, ct);

        if (isJoin && settings.IsLocked(LockType.Bots))
        {
            // Bots added by an admin are welcome; any other bot is removed straight away.
            var addedByAdmin = message.From is { } adder && await admins.IsAdminAsync(context.ChatId, adder.Id, ct);
            if (!addedByAdmin)
            {
                foreach (var bot in message.NewChatMembers!.Where(u => u.IsBot && u.Id != me.Id))
                    await actions.BanAsync(context.ChatId, bot.Id, null, ct);
            }
        }

        if (settings.IsLocked(LockType.Service)) await actions.DeleteAsync(context.ChatId, message.MessageId, ct);

        // Join messages carry on: the welcome plugin answers them later in the pipeline.
        return HandlerResult.Continue;
    }
}

/// <summary>
/// /lock photo sticker, /unlock photo, /locks — or «قفل عکس», «باز کردن عکس», «قفل‌ها». Admins only.
/// «قفل همه» (/lock all) closes everything; then the admin opens only what they want, e.g. «باز کردن عکس».
/// </summary>
public sealed class LockCommandsHandler(PluginStateStore states, BotActions actions) : IGroupMessageHandler
{
    private static readonly string LockWord = PersianNormalizer.Normalize("قفل");
    private static readonly string[] UnlockWords = new[] { "باز کردن", "بازکردن", "آزادسازی" }.Select(PersianNormalizer.Normalize).ToArray();
    private static readonly string[] ListWords = new[] { "قفل", "قفل‌ها", "قفلها", "قفل ها", "وضعیت قفل‌ها", "وضعیت قفل", "لیست قفل‌ها", "لیست قفل" }.Select(PersianNormalizer.Normalize).ToArray();

    private static readonly Localized ListTitle = new("وضعیت قفل‌های این گروه", "Locks in this group");
    private static readonly Localized ClosedLine = new("🔒 بسته: {0}", "🔒 Locked: {0}");
    private static readonly Localized OpenLine = new("🔓 آزاد: {0}", "🔓 Allowed: {0}");
    private static readonly Localized Nothing = new("هیچ", "none");
    private static readonly Localized ListHelp = new(
        "قفل کردن: «قفل عکس» یا /lock photo\nباز کردن: «باز کردن عکس» یا /unlock photo\nهمه با هم: «قفل همه» / «باز کردن همه»",
        "Lock: /lock photo — Unlock: /unlock photo — Everything: /lock all, /unlock all");
    private static readonly Localized Locked = new("🔒 قفل شد: {0}", "🔒 Locked: {0}");
    private static readonly Localized Unlocked = new("🔓 باز شد: {0}", "🔓 Unlocked: {0}");
    private static readonly Localized LockedAll = new(
        "🔒 همه چیز قفل شد. هر کدوم رو خواستی باز کنی بنویس، مثلاً «باز کردن عکس».",
        "🔒 Everything is locked. Unlock what you want, e.g. /unlock photo.");
    private static readonly Localized UnlockedAll = new("🔓 همه‌ی قفل‌ها باز شد.", "🔓 All locks are off.");
    private static readonly Localized Unknown = new(
        "❓ قفلی با این نام نیست. نام‌ها: {0}", "❓ No such lock. Names: {0}");

    private static readonly TimeSpan Lifetime = TimeSpan.FromSeconds(30);

    private enum Verb { List, Lock, Unlock }

    public string PluginId => LocksPlugin.Id;

    public int Order => HandlerOrder.Commands;

    public async Task<HandlerResult> HandleAsync(GroupMessageContext context, CancellationToken ct)
    {
        var request = ReadRequest(context);
        if (request is null) return HandlerResult.Continue;
        if (!context.SenderIsAdmin) return HandlerResult.Continue;

        var (verb, names) = request.Value;
        var chatId = context.ChatId;
        var lang = context.Group.Lang;
        var settings = await states.GetSettingsAsync<LocksSettings>(chatId, LocksPlugin.Id, ct);
        actions.ScheduleDelete(chatId, context.Message.MessageId, Lifetime);

        if (verb == Verb.List || names.Count == 0)
        {
            await actions.SendTemporaryAsync(chatId,
                $"<b>{ListTitle.Get(lang)}</b>\n{Status(settings, lang)}\n\n{ListHelp.Get(lang)}", TimeSpan.FromMinutes(1), ct: ct);
            return HandlerResult.Stop;
        }

        var types = names.Select(LockTypes.Expand).ToList();
        if (types.Any(t => t is null))
        {
            var known = string.Join("، ", LockTypes.All.Select(t => LockTypes.Keyword(t, lang)).Append(lang == Languages.English ? "all" : "همه"));
            await actions.SendTemporaryAsync(chatId, Unknown.Format(lang, known), Lifetime, ct: ct);
            return HandlerResult.Stop;
        }

        var changed = types.SelectMany(t => t!).Distinct().ToList();
        var updated = verb == Verb.Lock
            ? settings.Locked.Union(changed).ToList()
            : settings.Locked.Except(changed).ToList();
        var saved = new LocksSettings { Locked = updated, WarnOnViolation = settings.WarnOnViolation };
        await states.SaveSettingsAsync(chatId, LocksPlugin.Id, saved, ct);

        string reply;
        if (names.Any(LockTypes.IsAll))
            reply = (verb == Verb.Lock ? LockedAll : UnlockedAll).Get(lang);
        else
        {
            var list = string.Join("، ", changed.Select(t => LockTypes.Name(t, lang)));
            reply = (verb == Verb.Lock ? Locked : Unlocked).Format(lang, Html.Escape(list));
        }
        // Every change also shows the whole picture, so the admin always knows what is open and what is closed.
        await actions.SendTemporaryAsync(chatId, $"{reply}\n\n{Status(saved, lang)}", Lifetime, ct: ct);
        return HandlerResult.Stop;
    }

    /// <summary>Two lines: what is locked and what is allowed in this group.</summary>
    private static string Status(LocksSettings settings, string lang)
    {
        string Join(IEnumerable<LockType> types)
        {
            var names = types.Select(t => LockTypes.Name(t, lang)).ToList();
            return names.Count == 0 ? Nothing.Raw(lang) : Html.Escape(string.Join("، ", names));
        }

        return $"{ClosedLine.Format(lang, Join(LockTypes.All.Where(settings.IsLocked)))}\n" +
               OpenLine.Format(lang, Join(LockTypes.All.Where(t => !settings.IsLocked(t))));
    }

    /// <summary>What the admin asked for, or null if the message is not a lock command.</summary>
    private static (Verb Verb, List<string> Names)? ReadRequest(GroupMessageContext context)
    {
        if (context.Command is { } command)
        {
            var args = SplitNames(command.Args);
            return command.Name switch
            {
                "locks" => (Verb.List, args),
                "lock" => (args.Count == 0 ? Verb.List : Verb.Lock, args),
                "unlock" => (args.Count == 0 ? Verb.List : Verb.Unlock, args),
                _ => null,
            };
        }

        // Keywords: only when every word after «قفل» / «باز کردن» is a real lock name, so chat is left alone.
        var text = context.NormalizedText.Value;
        if (ListWords.Contains(text)) return (Verb.List, []);

        if (text.StartsWith(LockWord + " ", StringComparison.Ordinal))
            return AllKnown(text[(LockWord.Length + 1)..]) is { } locks ? (Verb.Lock, locks) : null;

        foreach (var word in UnlockWords)
        {
            if (text.StartsWith(word + " ", StringComparison.Ordinal))
                return AllKnown(text[(word.Length + 1)..]) is { } unlocks ? (Verb.Unlock, unlocks) : null;
        }
        return null;
    }

    private static List<string>? AllKnown(string rest)
    {
        var names = SplitNames(rest);
        return names.Count > 0 && names.All(n => LockTypes.Expand(n) is not null) ? names : null;
    }

    private static List<string> SplitNames(string text) =>
        text.Split([' ', ',', '،', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(n => n is not ("و" or "and" or "چیز" or "چی"))
            .ToList();
}
