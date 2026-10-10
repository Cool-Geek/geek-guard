using CoolGeek.PersianText;
using GeekGuard.Core.Groups;
using GeekGuard.Core.Messaging;
using GeekGuard.Core.Private;
using Telegram.Bot.Types;
using Telegram.Bot.Types.ReplyMarkups;

namespace GeekGuard.Modules.Welcome;

/// <summary>
/// The group's rules: admins set them, anyone can read them. In the group they come as a button that opens the
/// rules in a private chat with the bot, so long rules never fill the group.
/// </summary>
public sealed class RulesPlugin : IGeekGuardPlugin
{
    public const string Id = "rules";

    /// <summary>Start-link prefix: t.me/bot?start=rules_&lt;chat id&gt;.</summary>
    public const string LinkPrefix = "rules";

    public PluginManifest Manifest { get; } = new(Id, "قوانین", "Rules", PluginTier.Free, EnabledByDefault: true);

    public void ConfigureServices(IServiceCollection services, IConfiguration configuration)
    {
        services.AddGroupMessageHandler<RulesCommandsHandler>();
        services.AddStartLinkHandler<RulesLinkHandler>();
        services.AddPanelSection<RulesSection>();
    }

    /// <summary>The "📜 Rules" button, opening the rules in a private chat.</summary>
    public static InlineKeyboardMarkup Button(string botUsername, long chatId, string lang) =>
        new(InlineKeyboardButton.WithUrl(lang == Languages.English ? "📜 Rules" : "📜 قوانین گروه",
            StartLinks.For(botUsername, LinkPrefix, chatId.ToString())));
}

public sealed class RulesSettings
{
    public const int MaxLength = 3500;

    /// <summary>The rules as the admin wrote them (plain text). Empty: no rules set.</summary>
    public string Text { get; set; } = "";
}

/// <summary>
/// /rules or «قوانین»: anyone, answered with the rules button (members at most once a minute).
/// /setrules text, or «تنظیم قوانین» + text, or either as a reply to a message holding the rules: admins.
/// /clearrules or «حذف قوانین»: admins.
/// </summary>
public sealed class RulesCommandsHandler(PluginStateStore states, BotActions actions, NoticeThrottle throttle, BotIdentity me)
    : IGroupMessageHandler
{
    private static readonly string[] ShowWords = Normalized("قوانین", "قوانین گروه", "rules");
    private static readonly string[] SetPrefixes = Normalized("تنظیم قوانین", "ثبت قوانین");
    private static readonly string[] ClearWords = Normalized("حذف قوانین", "پاک کردن قوانین");

    private static readonly Localized Show = new("📜 قوانین این گروه را از دکمه‌ی زیر بخوانید.", "📜 Tap below to read this group's rules.");
    private static readonly Localized NoRules = new(
        "ℹ️ هنوز قانونی ثبت نشده.", "ℹ️ No rules have been set yet.");
    private static readonly Localized NoRulesAdmin = new(
        "ℹ️ هنوز قانونی ثبت نشده. برای ثبت: «تنظیم قوانین» و در خط‌های بعد متن قوانین، یا روی پیام قوانین ریپلای کن و بنویس «تنظیم قوانین».",
        "ℹ️ No rules yet. Set them with /setrules followed by the text, or reply /setrules to a message holding them.");
    private static readonly Localized Saved = new(
        "✅ قوانین ثبت شد ({0} حرف). اعضا با «قوانین» یا دکمه‌ی پیام خوشامد می‌بینندش.",
        "✅ Rules saved ({0} characters). Members can read them with /rules or from the welcome message.");
    private static readonly Localized TooLong = new("⚠️ قوانین حداکثر {0} حرف می‌تواند باشد.", "⚠️ Rules can be at most {0} characters.");
    private static readonly Localized Cleared = new("🗑 قوانین پاک شد.", "🗑 Rules removed.");

    private static readonly TimeSpan Lifetime = TimeSpan.FromSeconds(30);

    private enum Verb { Show, Set, Clear }

    public string PluginId => RulesPlugin.Id;

    public int Order => HandlerOrder.Commands;

    public async Task<HandlerResult> HandleAsync(GroupMessageContext context, CancellationToken ct)
    {
        if (ReadRequest(context) is not { } request) return HandlerResult.Continue;
        var (verb, text) = request;

        var chatId = context.ChatId;
        var lang = context.Group.Lang;
        var settings = await states.GetSettingsAsync<RulesSettings>(chatId, RulesPlugin.Id, ct);

        if (verb == Verb.Show)
        {
            if (!context.SenderIsAdmin)
            {
                // Members may ask too, but at most once a minute each, and only when there is something to show.
                if (settings.Text.Length == 0 || context.Message.From is not { } member
                    || !throttle.TryAcquire(chatId, member.Id, RulesPlugin.Id)) return HandlerResult.Continue;
            }

            actions.ScheduleDelete(chatId, context.Message.MessageId, Lifetime);
            if (settings.Text.Length == 0)
            {
                await actions.SendTemporaryAsync(chatId, NoRulesAdmin.Get(lang), Lifetime, ct: ct);
                return HandlerResult.Stop;
            }

            var shown = await actions.SendAsync(chatId, Show.Get(lang), keyboard: RulesPlugin.Button(me.Username, chatId, lang), ct: ct);
            if (shown is not null) actions.ScheduleDelete(chatId, shown.MessageId, TimeSpan.FromMinutes(2));
            return HandlerResult.Stop;
        }

        if (!context.SenderIsAdmin) return HandlerResult.Continue;
        actions.ScheduleDelete(chatId, context.Message.MessageId, Lifetime);

        if (verb == Verb.Clear)
        {
            await states.SaveSettingsAsync(chatId, RulesPlugin.Id, new RulesSettings(), ct);
            await actions.SendTemporaryAsync(chatId, Cleared.Get(lang), Lifetime, ct: ct);
            return HandlerResult.Stop;
        }

        // Set: the text after the command, or the message the admin replied to.
        var rules = text.Length > 0 ? text : (context.Message.ReplyToMessage?.Text ?? context.Message.ReplyToMessage?.Caption ?? "").Trim();
        if (rules.Length == 0)
        {
            await actions.SendTemporaryAsync(chatId, NoRulesAdmin.Get(lang), Lifetime, ct: ct);
            return HandlerResult.Stop;
        }
        if (rules.Length > RulesSettings.MaxLength)
        {
            await actions.SendTemporaryAsync(chatId, TooLong.Format(lang, RulesSettings.MaxLength), Lifetime, ct: ct);
            return HandlerResult.Stop;
        }

        await states.SaveSettingsAsync(chatId, RulesPlugin.Id, new RulesSettings { Text = rules }, ct);
        await actions.SendTemporaryAsync(chatId, Saved.Format(lang, rules.Length), Lifetime, ct: ct);
        return HandlerResult.Stop;
    }

    private static (Verb Verb, string Text)? ReadRequest(GroupMessageContext context)
    {
        if (context.Command is { } command)
        {
            return command.Name switch
            {
                "rules" => (Verb.Show, ""),
                "setrules" => (Verb.Set, command.Args.Trim()),
                "clearrules" => (Verb.Clear, ""),
                _ => null,
            };
        }

        var text = context.NormalizedText.Value;
        if (ShowWords.Contains(text)) return (Verb.Show, "");
        if (ClearWords.Contains(text)) return (Verb.Clear, "");
        if (SetPrefixes.Contains(text)) return (Verb.Set, "");

        // «تنظیم قوانین» followed by the rules (usually on the next lines): keep the admin's own text and line breaks.
        if (SetPrefixes.Any(p => text.StartsWith(p + " ", StringComparison.Ordinal)))
            return (Verb.Set, TextHelpers.AfterWords(context.Text, 2));
        return null;
    }

    private static string[] Normalized(params string[] words) => words.Select(PersianNormalizer.Normalize).ToArray();
}

/// <summary>Opens the rules of a group in the private chat, from the group's rules button.</summary>
public sealed class RulesLinkHandler(PluginStateStore states, GroupDirectory groups, BotActions actions) : IStartLinkHandler
{
    private static readonly Localized Title = new("📜 <b>قوانین «{0}»</b>", "📜 <b>Rules of “{0}”</b>");
    private static readonly Localized None = new("ℹ️ این گروه هنوز قانونی ثبت نکرده.", "ℹ️ This group has not set any rules yet.");

    public string Prefix => RulesPlugin.LinkPrefix;

    public async Task HandleAsync(Message message, string argument, CancellationToken ct)
    {
        if (!long.TryParse(argument, out var chatId) || await groups.GetAsync(chatId, ct) is not { } group) return;

        var lang = group.Lang;
        var settings = await states.GetSettingsAsync<RulesSettings>(chatId, RulesPlugin.Id, ct);
        var text = settings.Text.Length == 0
            ? None.Get(lang)
            : $"{Title.Format(lang, Html.Escape(group.Title))}\n\n{Html.Escape(settings.Text)}";
        await actions.SendAsync(message.Chat.Id, text, ct: ct);
    }
}

/// <summary>Small helpers for commands that carry free text.</summary>
public static class TextHelpers
{
    /// <summary>The original text after its first <paramref name="count"/> words, line breaks kept.</summary>
    public static string AfterWords(string text, int count) =>
        System.Text.RegularExpressions.Regex.Replace(text, $@"^\s*(\S+\s+){{{count}}}", "").Trim();
}
