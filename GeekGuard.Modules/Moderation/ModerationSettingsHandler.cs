using System.Globalization;
using CoolGeek.PersianText;
using GeekGuard.Core.Messaging;

namespace GeekGuard.Modules.Moderation;

/// <summary>
/// Lets admins change moderation settings from inside the group until the settings panel exists:
/// <c>/cleanup 20</c>, <c>/cleanup 1m</c>, or the keyword «پاکسازی ۲۰ ثانیه». With no value it shows the current one.
/// </summary>
public sealed class ModerationSettingsHandler(PluginStateStore states, BotActions actions) : IGroupMessageHandler
{
    private static readonly string CleanupKeyword = PersianNormalizer.Normalize("پاکسازی");

    private static readonly Localized Current = new(
        "🧹 پیام‌های مدیریتی بعد از {0} پاک می‌شوند.\nبرای تغییر: /cleanup 20 یا «پاکسازی ۲۰ ثانیه»",
        "🧹 Moderation messages are deleted after {0}.\nTo change: /cleanup 20");

    private static readonly Localized Changed = new(
        "✅ از این به بعد پیام‌های مدیریتی بعد از {0} پاک می‌شوند.",
        "✅ Moderation messages will now be deleted after {0}.");

    private static readonly Localized OutOfRange = new(
        "⏱ مدت باید بین ۵ ثانیه و ۱ روز باشد. مثلاً: /cleanup 20 یا /cleanup 2m",
        "⏱ Use a value between 5 seconds and 1 day, e.g. /cleanup 20 or /cleanup 2m");

    public string PluginId => ModerationPlugin.Id;

    public int Order => HandlerOrder.Commands;

    public async Task<HandlerResult> HandleAsync(GroupMessageContext context, CancellationToken ct)
    {
        var argument = ReadRequest(context);
        if (argument is null) return HandlerResult.Continue;

        // Settings are for admins. A member gets no reaction, and the message carries on to the filters
        // (stopping here would let "/cleanup t.me/spam" slip past anti-link).
        if (!context.SenderIsAdmin) return HandlerResult.Continue;

        var chatId = context.ChatId;
        var lang = context.Group.Lang;
        var settings = await states.GetSettingsAsync<ModerationSettings>(chatId, ModerationPlugin.Id, ct);
        actions.ScheduleDelete(chatId, context.Message.MessageId, settings.CleanupDelay);

        if (argument.Length == 0)
        {
            await actions.SendTemporaryAsync(chatId, Current.Format(lang, Duration.Format(settings.CleanupDelay, lang)),
                settings.CleanupDelay, ct: ct);
            return HandlerResult.Stop;
        }

        var seconds = ParseSeconds(argument);
        if (seconds is null or < ModerationSettings.MinCleanupSeconds or > ModerationSettings.MaxCleanupSeconds)
        {
            await actions.SendTemporaryAsync(chatId, OutOfRange.Get(lang), TimeSpan.FromSeconds(30), ct: ct);
            return HandlerResult.Stop;
        }

        // Settings objects are shared through the cache, so save a changed copy instead of mutating it.
        var updated = settings.Copy();
        updated.CleanupDelaySeconds = seconds.Value;
        await states.SaveSettingsAsync(chatId, ModerationPlugin.Id, updated, ct);
        await actions.SendTemporaryAsync(chatId, Changed.Format(lang, Duration.Format(updated.CleanupDelay, lang)),
            updated.CleanupDelay, ct: ct);
        return HandlerResult.Stop;
    }

    /// <summary>The argument ("" when none), or null when the message is not a cleanup request.</summary>
    private static string? ReadRequest(GroupMessageContext context)
    {
        if (context.Command is { Name: "cleanup" } command) return command.Args.Trim();

        // The keyword counts only alone or followed by a valid value, so "پاکسازی گروه رو انجام بدید" is just chat.
        var text = context.NormalizedText.Value;
        if (text == CleanupKeyword) return "";
        if (!text.StartsWith(CleanupKeyword + " ", StringComparison.Ordinal)) return null;
        var value = text[(CleanupKeyword.Length + 1)..];
        return ParseSeconds(value) is null ? null : value;
    }

    /// <summary>"20" means seconds; "20s", "2m", "۲۰ ثانیه", "1 دقیقه" are durations.</summary>
    private static int? ParseSeconds(string argument)
    {
        var normalized = PersianNormalizer.Normalize(argument);
        if (int.TryParse(normalized, NumberStyles.None, CultureInfo.InvariantCulture, out var plain)) return plain;
        return Duration.Parse(normalized, allowSeconds: true) is { } span ? (int)span.TotalSeconds : null;
    }
}
