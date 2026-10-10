using System.Collections.Concurrent;
using CoolGeek.PersianText;
using GeekGuard.Core.Messaging;
using Telegram.Bot.Types;

namespace GeekGuard.Modules.Welcome;

/// <summary>
/// Greets people who join, with the admins' own text and a rules button. Only the latest welcome stays in the chat,
/// so a wave of joins never buries the conversation.
/// </summary>
public sealed class WelcomePlugin : IGeekGuardPlugin
{
    public const string Id = "welcome";

    public PluginManifest Manifest { get; } = new(Id, "خوشامدگویی", "Welcome", PluginTier.Free, EnabledByDefault: true);

    public void ConfigureServices(IServiceCollection services, IConfiguration configuration)
    {
        services.AddSingleton<LastWelcomes>();
        services.AddGroupMessageHandler<WelcomeHandler>();
        services.AddGroupMessageHandler<WelcomeCommandsHandler>();
        services.AddPanelSection<WelcomeSection>();
    }
}

public sealed class WelcomeSettings
{
    public const int MaxLength = 1000;

    /// <summary>Placeholders admins can use in their text.</summary>
    public const string Placeholders = "{name} {group} {count}";

    public bool Enabled { get; set; } = true;

    /// <summary>The admins' text. Empty: the built-in greeting.</summary>
    public string Text { get; set; } = "";

    /// <summary>Remove the previous welcome when a new one is posted.</summary>
    public bool DeletePrevious { get; set; } = true;

    public static readonly Localized DefaultText = new(
        "👋 {name} عزیز، به «{group}» خوش اومدی!",
        "👋 Welcome to “{group}”, {name}!");

    /// <summary>
    /// The welcome as HTML. The admin's text is escaped first, then the placeholders are filled in,
    /// so nothing an admin types can break the message's formatting.
    /// </summary>
    public string Render(string lang, IReadOnlyList<User> newcomers, string groupTitle, int? memberCount)
    {
        var template = Text.Length > 0 ? Text : DefaultText.Get(lang);
        var names = string.Join(lang == Languages.English ? ", " : "، ", newcomers.Select(Html.Mention));
        return Html.Escape(template)
            .Replace("{name}", names)
            .Replace("{group}", Html.Escape(groupTitle))
            .Replace("{count}", memberCount?.ToString() ?? "");
    }
}

/// <summary>The welcome currently in each chat, so the next one can replace it. In memory: a restart forgets it.</summary>
public sealed class LastWelcomes
{
    private readonly ConcurrentDictionary<long, int> _last = new();

    /// <summary>Records the new welcome and returns the one it replaces, if any.</summary>
    public int? Swap(long chatId, int messageId)
    {
        int? previous = null;
        _last.AddOrUpdate(chatId, messageId, (_, old) => { previous = old; return messageId; });
        return previous;
    }
}

public sealed class WelcomeHandler(
    PluginStateStore states,
    BotActions actions,
    LastWelcomes lastWelcomes,
    BotIdentity me) : IGroupMessageHandler
{
    public string PluginId => WelcomePlugin.Id;

    // Right after the join locks, which may remove a bot that a member added.
    public int Order => HandlerOrder.Membership + 10;

    public async Task<HandlerResult> HandleAsync(GroupMessageContext context, CancellationToken ct)
    {
        // People, not bots; and not the bot itself joining (that is the "thanks for adding me" moment, not a welcome).
        var newcomers = (context.Message.NewChatMembers ?? []).Where(u => !u.IsBot).ToList();
        if (newcomers.Count == 0) return HandlerResult.Continue;

        var chatId = context.ChatId;
        var settings = await states.GetSettingsAsync<WelcomeSettings>(chatId, WelcomePlugin.Id, ct);
        if (!settings.Enabled) return HandlerResult.Continue;

        var lang = context.Group.Lang;
        var count = settings.Text.Contains("{count}") ? await actions.GetMemberCountAsync(chatId, ct) : null;
        var text = settings.Render(lang, newcomers, context.Message.Chat.Title ?? context.Group.Title, count);

        var rules = await states.GetSettingsAsync<RulesSettings>(chatId, RulesPlugin.Id, ct);
        var keyboard = rules.Text.Length > 0 ? RulesPlugin.Button(me.Username, chatId, lang) : null;

        var sent = await actions.SendAsync(chatId, text, keyboard: keyboard, ct: ct);
        if (sent is not null && lastWelcomes.Swap(chatId, sent.MessageId) is { } previous && settings.DeletePrevious)
            await actions.DeleteAsync(chatId, previous, ct);

        return HandlerResult.Continue;
    }
}

/// <summary>
/// Admins only:
/// /welcome or «خوشامد»: show the current welcome and how to change it;
/// /welcome on|off or «خوشامد روشن / خاموش»;
/// /setwelcome text or «تنظیم خوشامد» + text (or as a reply to a message holding it);
/// /resetwelcome or «خوشامد پیش‌فرض».
/// </summary>
public sealed class WelcomeCommandsHandler(PluginStateStore states, BotActions actions, BotIdentity me) : IGroupMessageHandler
{
    private static readonly string[] ShowWords = Normalized("خوشامد", "خوش آمد", "خوشامدگویی");
    private static readonly string[] OnWords = Normalized("خوشامد روشن", "خوشامدگویی روشن");
    private static readonly string[] OffWords = Normalized("خوشامد خاموش", "خوشامدگویی خاموش");
    private static readonly string[] SetPrefixes = Normalized("تنظیم خوشامد", "تنظیم خوشامدگویی");
    private static readonly string[] ResetWords = Normalized("خوشامد پیش فرض", "خوشامد پیشفرض");

    private static readonly Localized Status = new(
        "👋 خوشامدگویی: {0}\n\nپیش‌نمایش:\n{1}\n\nتغییر متن: «تنظیم خوشامد» و بعد متن دلخواه. می‌توانی از {2} استفاده کنی.\nروشن/خاموش: «خوشامد روشن» / «خوشامد خاموش» — متن پیش‌فرض: «خوشامد پیش‌فرض»",
        "👋 Welcome: {0}\n\nPreview:\n{1}\n\nChange it: /setwelcome followed by your text. You can use {2}.\n/welcome on | off — /resetwelcome for the default.");
    private static readonly Localized On = new("روشن ✅", "on ✅");
    private static readonly Localized Off = new("خاموش ❌", "off ❌");
    private static readonly Localized Changed = new("✅ خوشامدگویی {0} شد.", "✅ Welcome turned {0}.");
    private static readonly Localized Saved = new("✅ متن خوشامد ثبت شد. پیش‌نمایش:\n\n{0}", "✅ Welcome saved. Preview:\n\n{0}");
    private static readonly Localized Reset = new("✅ متن خوشامد به پیش‌فرض برگشت.", "✅ Welcome text reset to the default.");
    private static readonly Localized TooLong = new("⚠️ متن خوشامد حداکثر {0} حرف می‌تواند باشد.", "⚠️ The welcome can be at most {0} characters.");
    private static readonly Localized Empty = new(
        "ℹ️ متن خوشامد را بعد از «تنظیم خوشامد» بنویس، یا روی پیامی که متن در آن است ریپلای کن.",
        "ℹ️ Put the text after /setwelcome, or reply /setwelcome to a message holding it.");

    private static readonly TimeSpan Lifetime = TimeSpan.FromSeconds(30);

    private enum Verb { Show, On, Off, Set, Reset }

    public string PluginId => WelcomePlugin.Id;

    public int Order => HandlerOrder.Commands;

    public async Task<HandlerResult> HandleAsync(GroupMessageContext context, CancellationToken ct)
    {
        if (ReadRequest(context) is not { } request) return HandlerResult.Continue;
        if (!context.SenderIsAdmin) return HandlerResult.Continue;

        var (verb, text) = request;
        var chatId = context.ChatId;
        var lang = context.Group.Lang;
        var settings = await states.GetSettingsAsync<WelcomeSettings>(chatId, WelcomePlugin.Id, ct);
        actions.ScheduleDelete(chatId, context.Message.MessageId, Lifetime);

        // The preview greets the admin who asked, so they see exactly what newcomers will see.
        var sample = context.Message.From is { } from && context.Message.SenderChat is null
            ? new[] { from }
            : new[] { new User { Id = me.Id, FirstName = lang == Languages.English ? "New member" : "عضو جدید" } };
        var title = context.Message.Chat.Title ?? context.Group.Title;

        string reply;
        switch (verb)
        {
            case Verb.On or Verb.Off:
                await states.SaveSettingsAsync(chatId, WelcomePlugin.Id, Copy(settings, enabled: verb == Verb.On), ct);
                reply = Changed.Format(lang, (verb == Verb.On ? On : Off).Get(lang));
                break;

            case Verb.Reset:
                var reset = Copy(settings, text: "");
                await states.SaveSettingsAsync(chatId, WelcomePlugin.Id, reset, ct);
                reply = Reset.Get(lang);
                break;

            case Verb.Set:
                var welcome = text.Length > 0 ? text : (context.Message.ReplyToMessage?.Text ?? context.Message.ReplyToMessage?.Caption ?? "").Trim();
                if (welcome.Length == 0) { reply = Empty.Get(lang); break; }
                if (welcome.Length > WelcomeSettings.MaxLength) { reply = TooLong.Format(lang, WelcomeSettings.MaxLength); break; }

                // Setting a text also switches the welcome on: that is clearly what the admin wants.
                var saved = Copy(settings, text: welcome, enabled: true);
                await states.SaveSettingsAsync(chatId, WelcomePlugin.Id, saved, ct);
                reply = Saved.Format(lang, saved.Render(lang, sample, title, null));
                break;

            default:
                reply = Status.Format(lang, (settings.Enabled ? On : Off).Get(lang),
                    settings.Render(lang, sample, title, null), WelcomeSettings.Placeholders);
                break;
        }

        await actions.SendTemporaryAsync(chatId, reply, verb == Verb.Show ? TimeSpan.FromMinutes(1) : Lifetime, ct: ct);
        return HandlerResult.Stop;
    }

    private static WelcomeSettings Copy(WelcomeSettings s, string? text = null, bool? enabled = null) => new()
    {
        Text = text ?? s.Text,
        Enabled = enabled ?? s.Enabled,
        DeletePrevious = s.DeletePrevious,
    };

    private static (Verb Verb, string Text)? ReadRequest(GroupMessageContext context)
    {
        if (context.Command is { } command)
        {
            var args = command.Args.Trim();
            return command.Name switch
            {
                "welcome" => args.ToLowerInvariant() switch
                {
                    "on" => (Verb.On, ""),
                    "off" => (Verb.Off, ""),
                    _ => (Verb.Show, ""),
                },
                "setwelcome" => (Verb.Set, args),
                "resetwelcome" => (Verb.Reset, ""),
                _ => null,
            };
        }

        var text = context.NormalizedText.Value;
        if (ShowWords.Contains(text)) return (Verb.Show, "");
        if (OnWords.Contains(text)) return (Verb.On, "");
        if (OffWords.Contains(text)) return (Verb.Off, "");
        if (ResetWords.Contains(text)) return (Verb.Reset, "");
        if (SetPrefixes.Contains(text)) return (Verb.Set, "");
        if (SetPrefixes.Any(p => text.StartsWith(p + " ", StringComparison.Ordinal)))
            return (Verb.Set, TextHelpers.AfterWords(context.Text, 2));
        return null;
    }

    private static string[] Normalized(params string[] words) => words.Select(PersianNormalizer.Normalize).ToArray();
}
