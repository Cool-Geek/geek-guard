using CoolGeek.PersianText;
using GeekGuard.Core.Groups;
using GeekGuard.Core.Messaging;
using GeekGuard.Core.Panel;
using GeekGuard.Modules.Moderation;
using Telegram.Bot.Types.ReplyMarkups;

namespace GeekGuard.Modules.GroupClose;

/// <summary>
/// Close the group by hand: «بستن گروه», «بستن گروه ۲ ساعت», «باز کردن گروه» (or /close, /close 2h, /open).
/// While closed only admins can post. Closing on a schedule (night mode) is a Pro plugin built on the same
/// <see cref="GroupCloser"/>.
/// </summary>
public sealed class GroupClosePlugin : IGeekGuardPlugin
{
    public const string Id = "group-close";

    public PluginManifest Manifest { get; } = new(Id, "بستن گروه", "Close group", PluginTier.Free, EnabledByDefault: true);

    public void ConfigureServices(IServiceCollection services, IConfiguration configuration)
    {
        services.AddGroupMessageHandler<GroupCloseCommandsHandler>();
        services.AddPanelSection<GroupCloseSection>();
        services.AddHelpTopic(Help.HelpTopics.GroupClose);
        services.AddBotCommands(Help.CommandMenu.GroupClose);
    }

    internal static readonly Localized NotClosed = new("ℹ️ گروه بسته نیست.", "ℹ️ The group is not closed.");
    internal static readonly Localized Usage = new(
        "⏱ زمان را این‌طور بنویس: «بستن گروه ۲ ساعت» یا /close 30m",
        "⏱ Give the time like this: /close 30m or /close 2h");
}

public sealed class GroupCloseCommandsHandler(GroupCloser closer, GroupAdmins admins, BotActions actions) : IGroupMessageHandler
{
    private static readonly string[] ClosePrefixes = Normalized("بستن گروه", "قفل گروه");
    private static readonly string[] OpenWords = Normalized("باز کردن گروه", "بازکردن گروه", "باز شدن گروه");

    private static readonly TimeSpan HintLifetime = TimeSpan.FromSeconds(30);

    public string PluginId => GroupClosePlugin.Id;

    public int Order => HandlerOrder.Commands;

    public async Task<HandlerResult> HandleAsync(GroupMessageContext context, CancellationToken ct)
    {
        if (Read(context) is not { } request) return HandlerResult.Continue;
        if (!context.SenderIsAdmin) return HandlerResult.Continue;

        var chatId = context.ChatId;
        var lang = context.Group.Lang;
        actions.ScheduleDelete(chatId, context.Message.MessageId, HintLifetime);

        // Same rule as mute and ban: closing the group needs the "restrict members" right.
        var allowed = context.IsAnonymousAdmin
                      || (context.Message is { SenderChat: null, From: { } from } && await admins.CanRestrictAsync(chatId, from.Id, ct));
        if (!allowed)
        {
            await actions.SendTemporaryAsync(chatId, ModerationTexts.OnlyAdmins.Get(lang), HintLifetime, ct: ct);
            return HandlerResult.Stop;
        }

        var (open, duration, invalid) = request;
        if (invalid)
        {
            await actions.SendTemporaryAsync(chatId, GroupClosePlugin.Usage.Get(lang), HintLifetime, ct: ct);
            return HandlerResult.Stop;
        }

        if (open)
        {
            var opened = await closer.OpenAsync(chatId, lang, ct);
            if (opened is null) await actions.SendTemporaryAsync(chatId, GroupClosePlugin.NotClosed.Get(lang), HintLifetime, ct: ct);
            else if (!opened.Value.Succeeded) await actions.SendTemporaryAsync(chatId, ModerationTexts.ForProblem(opened.Value, lang), TimeSpan.FromMinutes(1), ct: ct);
            return HandlerResult.Stop;
        }

        var reopenAt = duration is { } d ? DateTimeOffset.UtcNow + d : (DateTimeOffset?)null;
        var result = await closer.CloseAsync(chatId, lang, GroupCloser.Manual, reopenAt, ct: ct);
        if (!result.Succeeded) await actions.SendTemporaryAsync(chatId, ModerationTexts.ForProblem(result, lang), TimeSpan.FromMinutes(1), ct: ct);
        return HandlerResult.Stop;
    }

    /// <summary>Open?, how long to close, and whether the time could not be read. Null: not a command.</summary>
    private static (bool Open, TimeSpan? Duration, bool Invalid)? Read(GroupMessageContext context)
    {
        if (context.Command is { } command)
        {
            var args = command.Args.Trim();
            return command.Name switch
            {
                "open" => (true, null, false),
                "close" when args.Length == 0 => (false, null, false),
                "close" => Duration.Parse(args) is { } d ? (false, d, false) : (false, null, true),
                _ => null,
            };
        }

        var text = context.NormalizedText.Value;
        if (OpenWords.Contains(text)) return (true, null, false);
        if (ClosePrefixes.Contains(text)) return (false, null, false);

        // «بستن گروه ۲ ساعت»: only with a real duration, so chat about closing the group is left alone.
        foreach (var prefix in ClosePrefixes)
        {
            if (text.StartsWith(prefix + " ", StringComparison.Ordinal) && Duration.Parse(text[(prefix.Length + 1)..]) is { } d)
                return (false, d, false);
        }
        return null;
    }

    private static string[] Normalized(params string[] words) => words.Select(PersianNormalizer.Normalize).ToArray();
}

/// <summary>Panel: close now, close for a while, or open.</summary>
public sealed class GroupCloseSection(GroupCloser closer, TimeProvider clock) : IPanelSection
{
    private static readonly Localized Text = new(
        "🔐 <b>بستن گروه</b>\n\nوقتی گروه بسته است فقط ادمین‌ها می‌توانند پیام بدهند. موقع باز شدن، دسترسی‌های قبلی گروه دقیقاً برمی‌گردد.\n\nوضعیت: <b>{0}</b>",
        "🔐 <b>Close group</b>\n\nWhile the group is closed only admins can post. When it opens, the group's previous permissions come back exactly.\n\nStatus: <b>{0}</b>");
    private static readonly Localized Open = new("باز 🔓", "open 🔓");
    private static readonly Localized Closed = new("بسته 🔒", "closed 🔒");
    private static readonly Localized ClosedUntil = new("بسته 🔒 تا {0}", "closed 🔒 until {0}");
    private static readonly Localized ClosedBy = new(" (خودکار)", " (scheduled)");

    private static readonly int[] MinuteChoices = [30, 60, 120, 480];

    public string Id => "close";

    public string PluginId => GroupClosePlugin.Id;

    public int Order => 45;

    public Localized Title { get; } = new("🔐 بستن گروه", "🔐 Close group");

    public bool HasPluginSwitch => false;

    public async Task<PanelView> ShowAsync(PanelContext context, CancellationToken ct)
    {
        var lang = context.Lang;
        var fa = lang != Languages.English;
        var closure = await closer.GetAsync(context.ChatId, ct);

        var status = closure is null ? Open.Raw(lang)
            : closure.ReopenAt is { } at ? ClosedUntil.Raw(lang).Replace("{0}", LocalTime.When(new DateTimeOffset(at, TimeSpan.Zero), lang, clock))
            : Closed.Raw(lang);
        if (closure is not null && closure.Source != GroupCloser.Manual) status += ClosedBy.Raw(lang);

        var rows = new List<InlineKeyboardButton[]>();
        if (closure is null)
        {
            rows.Add(new[] { PanelButtons.Danger(fa ? "🔒 بستن تا وقتی باز کنم" : "🔒 Close until I open it", context, Id, "close") });
            rows.Add(PanelButtons.Header(fa ? "⏱ بستن برای مدت" : "⏱ Close for"));
            rows.Add(MinuteChoices
                .Select(m => PanelButtons.Action(Duration.Format(TimeSpan.FromMinutes(m), lang), context, Id, "for", m.ToString()))
                .ToArray());
        }
        else
        {
            rows.Add(new[] { PanelButtons.Primary(PanelButtons.Action(fa ? "🔓 باز کردن گروه" : "🔓 Open the group", context, Id, "open")) });
        }
        rows.Add(new[] { PanelButtons.BackToGroup(context) });

        return new PanelView(context.T(Text).Replace("{0}", status), new InlineKeyboardMarkup(rows));
    }

    public async Task<string?> HandleAsync(PanelContext context, string[] args, CancellationToken ct)
    {
        // Notices in the group are in the group's language; the note back to the admin in theirs.
        var groupLang = context.Group.Lang;
        ActionResult? result = args[0] switch
        {
            "close" => await closer.CloseAsync(context.ChatId, groupLang, GroupCloser.Manual, null, ct: ct),
            "for" when args.Length > 1 && int.TryParse(args[1], out var minutes) && MinuteChoices.Contains(minutes) =>
                await closer.CloseAsync(context.ChatId, groupLang, GroupCloser.Manual, clock.GetUtcNow().AddMinutes(minutes), ct: ct),
            "open" => await closer.OpenAsync(context.ChatId, groupLang, ct),
            _ => ActionResult.Ok,
        };

        return result switch
        {
            null => GroupClosePlugin.NotClosed.Get(context.Lang),
            { Succeeded: false } failed => StripTags(ModerationTexts.ForProblem(failed, context.Lang)),
            _ => null,
        };
    }

    private static string StripTags(string html) =>
        Html.Truncate(System.Net.WebUtility.HtmlDecode(System.Text.RegularExpressions.Regex.Replace(html, "<[^>]+>", "")), 190);
}
