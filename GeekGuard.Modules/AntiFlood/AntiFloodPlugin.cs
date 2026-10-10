using CoolGeek.PersianText;
using GeekGuard.Core.Messaging;
using GeekGuard.Modules.Moderation;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace GeekGuard.Modules.AntiFlood;

/// <summary>
/// Stops members from flooding the chat: too many messages in 10 seconds removes the whole burst and
/// mutes the sender for a while. Admins are exempt.
/// </summary>
public sealed class AntiFloodPlugin : IGeekGuardPlugin
{
    public const string Id = "anti-flood";

    public PluginManifest Manifest { get; } = new(Id, "ضدفلود", "Anti-flood", PluginTier.Free, EnabledByDefault: true);

    public void ConfigureServices(IServiceCollection services, IConfiguration configuration)
    {
        services.TryAddSingleton<FloodTracker>();
        services.AddGroupMessageHandler<AntiFloodHandler>();
        services.AddGroupMessageHandler<AntiFloodSettingsHandler>();
        services.AddPanelSection<AntiFloodSection>();
    }
}

public enum FloodLevel
{
    Off = 0,
    Low = 1,
    Medium = 2,
    Strict = 3,
}

/// <summary>Per-group anti-flood settings.</summary>
public sealed class AntiFloodSettings
{
    public static readonly TimeSpan Window = TimeSpan.FromSeconds(10);

    public FloodLevel Level { get; set; } = FloodLevel.Medium;

    /// <summary>How long a flooder is muted.</summary>
    public int MuteMinutes { get; set; } = 10;

    /// <summary>Messages within <see cref="Window"/> that count as flooding.</summary>
    public static int LimitFor(FloodLevel level) => level switch
    {
        FloodLevel.Low => 10,
        FloodLevel.Medium => 7,
        FloodLevel.Strict => 5,
        _ => int.MaxValue,
    };
}

public sealed class AntiFloodHandler(
    FloodTracker tracker,
    PluginStateStore states,
    BotActions actions,
    NoticeThrottle throttle) : IGroupMessageHandler
{
    private static readonly Localized Muted = new(
        "🔇 {0} به دلیل ارسال پیام‌های پشت‌سرهم برای {1} ساکت شد.",
        "🔇 {0} was muted for {1} for flooding.");

    public string PluginId => AntiFloodPlugin.Id;

    public int Order => HandlerOrder.Flood;

    public async Task<HandlerResult> HandleAsync(GroupMessageContext context, CancellationToken ct)
    {
        // Admins and members posting as a channel (no user to mute) are not counted.
        if (context.SenderIsAdmin || context.Message is not { SenderChat: null, From: { } member }) return HandlerResult.Continue;

        var settings = await states.GetSettingsAsync<AntiFloodSettings>(context.ChatId, AntiFloodPlugin.Id, ct);
        if (settings.Level == FloodLevel.Off) return HandlerResult.Continue;

        var burst = tracker.Hit(context.ChatId, member.Id, context.Message.MessageId,
            AntiFloodSettings.LimitFor(settings.Level), AntiFloodSettings.Window);
        if (burst is null) return HandlerResult.Continue;

        foreach (var messageId in burst) await actions.DeleteAsync(context.ChatId, messageId, ct);

        var muteFor = TimeSpan.FromMinutes(Math.Clamp(settings.MuteMinutes, 1, 1440));
        var result = await actions.MuteAsync(context.ChatId, member.Id, muteFor, ct);
        if (result.Succeeded && throttle.TryAcquire(context.ChatId, member.Id, AntiFloodPlugin.Id))
        {
            var lang = context.Group.Lang;
            await actions.SendTemporaryAsync(context.ChatId,
                Muted.Format(lang, Html.Mention(member), Duration.Format(muteFor, lang)), TimeSpan.FromSeconds(30), ct: ct);
        }
        return HandlerResult.Stop;
    }
}

/// <summary>
/// /flood shows the level; /flood off|low|medium|strict (or خاموش|کم|متوسط|سخت) changes it.
/// Keyword form: «ضدفلود سخت». Admins only.
/// </summary>
public sealed class AntiFloodSettingsHandler(PluginStateStore states, BotActions actions) : IGroupMessageHandler
{
    private static readonly string Keyword = PersianNormalizer.Normalize("ضدفلود");

    private static readonly Localized Current = new(
        "🌊 ضدفلود: {0}\nبرای تغییر: /flood off | low | medium | strict یا «ضدفلود خاموش / کم / متوسط / سخت»",
        "🌊 Anti-flood: {0}\nTo change: /flood off | low | medium | strict");

    private static readonly Localized Changed = new("✅ ضدفلود: {0}", "✅ Anti-flood: {0}");

    public string PluginId => AntiFloodPlugin.Id;

    public int Order => HandlerOrder.Commands;

    public async Task<HandlerResult> HandleAsync(GroupMessageContext context, CancellationToken ct)
    {
        var argument = ReadRequest(context);
        if (argument is null) return HandlerResult.Continue;
        if (!context.SenderIsAdmin) return HandlerResult.Continue;

        var chatId = context.ChatId;
        var lang = context.Group.Lang;
        var settings = await states.GetSettingsAsync<AntiFloodSettings>(chatId, AntiFloodPlugin.Id, ct);
        var cleanup = TimeSpan.FromSeconds(30);
        actions.ScheduleDelete(chatId, context.Message.MessageId, cleanup);

        if (ParseLevel(argument) is not { } level)
        {
            await actions.SendTemporaryAsync(chatId, Current.Format(lang, Describe(settings.Level, lang)), cleanup, ct: ct);
            return HandlerResult.Stop;
        }

        await states.SaveSettingsAsync(chatId, AntiFloodPlugin.Id,
            new AntiFloodSettings { Level = level, MuteMinutes = settings.MuteMinutes }, ct);
        await actions.SendTemporaryAsync(chatId, Changed.Format(lang, Describe(level, lang)), cleanup, ct: ct);
        return HandlerResult.Stop;
    }

    private static string? ReadRequest(GroupMessageContext context)
    {
        if (context.Command is { Name: "flood" } command) return command.Args.Trim();

        var text = context.NormalizedText.Value;
        if (text == Keyword) return "";
        // Keyword with a value only when the value is a real level, so ordinary chat is left alone.
        return text.StartsWith(Keyword + " ", StringComparison.Ordinal) && ParseLevel(text[(Keyword.Length + 1)..]) is not null
            ? text[(Keyword.Length + 1)..]
            : null;
    }

    private static FloodLevel? ParseLevel(string text) => PersianNormalizer.Normalize(text) switch
    {
        "off" or "خاموش" => FloodLevel.Off,
        "low" or "کم" => FloodLevel.Low,
        "medium" or "متوسط" => FloodLevel.Medium,
        "strict" or "high" or "سخت" or "زیاد" => FloodLevel.Strict,
        _ => null,
    };

    private static string Describe(FloodLevel level, string lang)
    {
        var fa = lang != Languages.English;
        return level switch
        {
            FloodLevel.Off => fa ? "خاموش" : "off",
            _ => fa
                ? $"{Name(level, true)} ({AntiFloodSettings.LimitFor(level)} پیام در ۱۰ ثانیه)"
                : $"{Name(level, false)} ({AntiFloodSettings.LimitFor(level)} messages in 10 s)",
        };
    }

    private static string Name(FloodLevel level, bool fa) => level switch
    {
        FloodLevel.Low => fa ? "کم" : "low",
        FloodLevel.Medium => fa ? "متوسط" : "medium",
        _ => fa ? "سخت" : "strict",
    };
}
