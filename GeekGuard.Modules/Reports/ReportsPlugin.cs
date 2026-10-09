using System.Collections.Concurrent;
using CoolGeek.PersianText;
using GeekGuard.Core.Callbacks;
using GeekGuard.Core.Groups;
using GeekGuard.Core.Messaging;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;

namespace GeekGuard.Modules.Reports;

/// <summary>
/// Lets members report a message to the admins: reply to it with /report or «گزارش». Admins get the report in
/// a private chat with the bot, so the group stays quiet and nobody sees who reported whom.
/// </summary>
public sealed class ReportsPlugin : IGeekGuardPlugin
{
    public const string Id = "reports";

    public PluginManifest Manifest { get; } = new(Id, "گزارش به ادمین", "Reports", PluginTier.Free, EnabledByDefault: true);

    public void ConfigureServices(IServiceCollection services, IConfiguration configuration)
    {
        services.AddSingleton<ReportLog>();
        services.AddSingleton<ReportStore>();
        services.AddGroupMessageHandler<ReportHandler>();
        services.AddCallbackHandler<ReportButtonHandler>();
        services.AddPluginMigrations(Id, GetType());
    }
}

/// <summary>Remembers recently reported messages, so a message reported by ten members reaches the admins once.</summary>
public sealed class ReportLog(TimeProvider clock)
{
    private static readonly TimeSpan Memory = TimeSpan.FromHours(1);

    private readonly ConcurrentDictionary<(long Chat, int Message), DateTimeOffset> _reported = new();

    /// <summary>True the first time a message is reported (within an hour).</summary>
    public bool TryAdd(long chatId, int messageId)
    {
        var now = clock.GetUtcNow();
        if (_reported.Count > 10_000)
        {
            foreach (var (key, at) in _reported)
                if (now - at > Memory) _reported.TryRemove(key, out _);
        }

        var added = false;
        _reported.AddOrUpdate((chatId, messageId),
            _ => { added = true; return now; },
            (_, at) => { if (now - at <= Memory) return at; added = true; return now; });
        return added;
    }
}

public sealed class ReportHandler(
    GroupAdmins admins,
    BotActions actions,
    NoticeThrottle throttle,
    ReportLog reports,
    ReportStore store,
    BotIdentity me) : IGroupMessageHandler
{
    private static readonly string[] Keywords = new[] { "گزارش", "ریپورت", "report" }.Select(PersianNormalizer.Normalize).ToArray();

    /// <summary>One report per member per minute, so nobody can flood the admins' private chats.</summary>
    private static readonly TimeSpan ReportWindow = TimeSpan.FromMinutes(1);

    private static readonly TimeSpan ConfirmLifetime = TimeSpan.FromSeconds(15);

    private static readonly Localized Sent = new(
        "✅ {0}، گزارشت به ادمین‌ها رسید. ممنون!", "✅ {0}, your report reached the admins. Thanks!");
    private static readonly Localized ReplyNeeded = new(
        "↩️ {0}، برای گزارش روی همان پیام ریپلای کن و بنویس «گزارش».",
        "↩️ {0}, reply to the message you want to report with /report.");
    private static readonly Localized TooSoon = new(
        "⏳ {0}، هر دقیقه فقط یک گزارش می‌شود فرستاد. {1} ثانیه دیگر دوباره امتحان کن.",
        "⏳ {0}, one report per minute. Try again in {1} seconds.");
    private static readonly Localized NobodyReachable = new(
        "⚠️ گزارش ثبت شد، اما هیچ‌کدام از ادمین‌ها ربات را استارت نکرده‌اند.\nادمین‌ها: برای دریافت گزارش‌ها یک بار به @{0} پیام /start بدهید.",
        "⚠️ Report received, but no admin has started the bot yet.\nAdmins: send /start to @{0} once to receive reports.");

    private static readonly Localized Title = new("🚩 <b>گزارش جدید</b> در «{0}»", "🚩 <b>New report</b> in “{0}”");
    private static readonly Localized Reporter = new("گزارش‌دهنده: {0}", "Reported by: {0}");
    private static readonly Localized Author = new("نویسنده‌ی پیام: {0}", "Message by: {0}");
    private static readonly Localized Content = new("پیام: {0}", "Message: {0}");
    private static readonly Localized Reason = new("توضیح: {0}", "Note: {0}");
    private static readonly Localized Open = new("🔗 <a href=\"{0}\">رفتن به پیام</a>", "🔗 <a href=\"{0}\">Open the message</a>");
    private static readonly Localized Media = new("[{0} بدون متن]", "[{0} without text]");

    public string PluginId => ReportsPlugin.Id;

    public int Order => HandlerOrder.Commands;

    public async Task<HandlerResult> HandleAsync(GroupMessageContext context, CancellationToken ct)
    {
        if (!IsReport(context, out var note)) return HandlerResult.Continue;

        var message = context.Message;
        var chatId = context.ChatId;
        var lang = context.Group.Lang;

        // A member posting as a channel has nobody to thank or to limit.
        if (message is not { SenderChat: null, From: { } reporter }) return HandlerResult.Continue;

        // The command never stays in the group: nobody needs to see who reported.
        await actions.DeleteAsync(chatId, message.MessageId, ct);

        if (message.ReplyToMessage is not { } reported || reported.From?.Id == me.Id)
        {
            // How-to hint, at most once a minute per member; it does not use up their report.
            if (throttle.TryAcquire(chatId, reporter.Id, ReportsPlugin.Id + ":hint"))
                await actions.SendTemporaryAsync(chatId, ReplyNeeded.Format(lang, Html.Mention(reporter)), ConfirmLifetime, ct: ct);
            return HandlerResult.Stop;
        }

        if (!throttle.TryAcquire(chatId, reporter.Id, ReportsPlugin.Id, out var wait, ReportWindow))
        {
            // Say why once; further attempts in the same minute are removed quietly.
            if (throttle.TryAcquire(chatId, reporter.Id, ReportsPlugin.Id + ":wait", ReportWindow))
                await actions.SendTemporaryAsync(chatId,
                    TooSoon.Format(lang, Html.Mention(reporter), Math.Max(1, (int)Math.Ceiling(wait.TotalSeconds))), ConfirmLifetime, ct: ct);
            return HandlerResult.Stop;
        }

        // Already reported by someone else in the last hour: the admins know; just thank this member.
        if (reports.TryAdd(chatId, reported.MessageId))
        {
            var delivered = await NotifyAdminsAsync(context, reporter, reported, note, ct);
            if (delivered == 0)
            {
                await actions.SendTemporaryAsync(chatId, NobodyReachable.Format(lang, me.Username), TimeSpan.FromMinutes(1), ct: ct);
                return HandlerResult.Stop;
            }
        }

        await actions.SendTemporaryAsync(chatId, Sent.Format(lang, Html.Mention(reporter)), ConfirmLifetime, ct: ct);
        return HandlerResult.Stop;
    }

    /// <summary>Sends the report to every admin who has started the bot. Returns how many received it.</summary>
    private async Task<int> NotifyAdminsAsync(GroupMessageContext context, User reporter, Message reported, string? note,
        CancellationToken ct)
    {
        var lang = context.Group.Lang;
        var author = reported is { SenderChat: null, From: { } user } ? user.Id : (long?)null;
        var reportId = await store.CreateAsync(context.ChatId, reported.MessageId, author, reporter.Id, lang, ct);

        var text = BuildReport(context, reporter, reported, note);
        var keyboard = ReportButtons.Open(reportId, lang, hasMember: author is not null);
        var delivered = 0;
        foreach (var adminId in await admins.GetAdminIdsAsync(context.ChatId, ct))
        {
            if (adminId == me.Id || adminId == GroupAdmins.AnonymousAdminId) continue;

            // Telegram refuses private messages to admins who never started the bot; they are simply skipped.
            if (await actions.SendAsync(adminId, text, keyboard: keyboard, ct: ct) is { } sent)
            {
                await store.AddDeliveryAsync(reportId, adminId, sent.MessageId, ct);
                delivered++;
            }
        }
        return delivered;
    }

    private static string BuildReport(GroupMessageContext context, User reporter, Message reported, string? note)
    {
        var lang = context.Group.Lang;
        var lines = new List<string>
        {
            Title.Format(lang, Html.Escape(Html.Truncate(context.Message.Chat.Title ?? "", 60))),
            "",
            Reporter.Format(lang, Html.Mention(reporter)),
        };

        if (reported is { SenderChat: null, From: { } author })
            lines.Add(Author.Format(lang, Html.MentionWithId(author)));
        else if (reported.SenderChat is { } channel)
            lines.Add(Author.Format(lang, Html.Escape(channel.Title ?? channel.Username ?? channel.Id.ToString())));

        lines.Add(Content.Format(lang, Describe(reported, lang)));
        if (!string.IsNullOrWhiteSpace(note)) lines.Add(Reason.Format(lang, Html.Escape(Html.Truncate(note, 200))));
        if (LinkTo(context.Message.Chat, reported.MessageId) is { } link) lines.Add(Open.Format(lang, link));
        return string.Join('\n', lines);
    }

    /// <summary>A short quote of the reported message, or what kind of media it was.</summary>
    private static string Describe(Message message, string lang)
    {
        var text = message.Text ?? message.Caption;
        if (!string.IsNullOrWhiteSpace(text)) return $"«{Html.Escape(Html.Truncate(text.Trim(), 300))}»";

        var fa = lang != Languages.English;
        var kind = message.Type switch
        {
            MessageType.Photo => fa ? "عکس" : "Photo",
            MessageType.Video or MessageType.VideoNote => fa ? "ویدیو" : "Video",
            MessageType.Sticker => fa ? "استیکر" : "Sticker",
            MessageType.Animation => fa ? "گیف" : "GIF",
            MessageType.Voice or MessageType.Audio => fa ? "صدا" : "Audio",
            MessageType.Document => fa ? "فایل" : "File",
            _ => fa ? "پیام" : "Message",
        };
        return Media.Format(lang, kind);
    }

    /// <summary>
    /// A link that opens the message for admins: t.me/name/123 for public groups, t.me/c/… for private supergroups.
    /// Basic groups have no message links.
    /// </summary>
    private static string? LinkTo(Chat chat, int messageId)
    {
        if (!string.IsNullOrEmpty(chat.Username)) return $"https://t.me/{chat.Username}/{messageId}";
        if (chat.Type != ChatType.Supergroup) return null;

        var id = chat.Id.ToString();
        return id.StartsWith("-100", StringComparison.Ordinal) ? $"https://t.me/c/{id[4..]}/{messageId}" : null;
    }

    /// <summary>/report [note] or a bare «گزارش». A keyword with more words after it is just chat.</summary>
    private static bool IsReport(GroupMessageContext context, out string? note)
    {
        note = null;
        if (context.Command is { Name: "report" } command)
        {
            note = command.Args.Length > 0 ? command.Args : null;
            return true;
        }
        return Keywords.Contains(context.NormalizedText.Value);
    }
}
