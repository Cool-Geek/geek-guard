using System.Collections.Concurrent;
using GeekGuard.Core.Licensing;
using GeekGuard.Core.Messaging;

namespace GeekGuard.Modules.GroupSettings;

/// <summary>
/// /activate GF-XXXX-XXXX-XXXX (admins): applies an activation code bought on the Geek Forge shop to this group.
/// The shop ties the code to the group; the days are then added here, to the group's Pro or to one plugin.
/// </summary>
public sealed class ActivateCommandHandler(
    ILicenseActivator shop,
    LicenseRedemptions redemptions,
    PluginCatalog catalog,
    BotActions actions,
    TimeProvider clock) : IGroupMessageHandler
{
    private static readonly TimeSpan AnswerLifetime = TimeSpan.FromMinutes(2);

    // A few tries per group per window: codes can't be guessed by flooding the command.
    private const int MaxTries = 5;
    private static readonly TimeSpan TryWindow = TimeSpan.FromMinutes(10);
    private readonly ConcurrentDictionary<long, (int Count, DateTimeOffset Since)> _tries = new();

    private static readonly Localized Usage = new(
        "💎 کد خریدشده از سایت را این‌طور بفرست:\n<code>/activate GF-XXXX-XXXX-XXXX</code>",
        "💎 Send the code you bought on the website like this:\n<code>/activate GF-XXXX-XXXX-XXXX</code>");
    private static readonly Localized TooMany = new(
        "⏳ چند بار پشت هم امتحان شد. چند دقیقه بعد دوباره بفرست.",
        "⏳ Too many tries. Send it again in a few minutes.");
    private static readonly Localized NotFound = new(
        "❌ این کد پیدا نشد. حروف و عددها را دوباره چک کن؛ کد در حساب کاربری‌ات در سایت، بخش «کدهای فعال‌سازی» است.",
        "❌ This code was not found. Check it again; it is under “Activation codes” in your account on the website.");
    private static readonly Localized UsedElsewhere = new(
        "⛔️ این کد قبلاً روی گروه دیگری فعال شده. هر کد فقط برای یک گروه است.",
        "⛔️ This code was already activated in another group. Each code is for one group only.");
    private static readonly Localized Revoked = new(
        "⛔️ این کد باطل شده و دیگر کار نمی‌کند.",
        "⛔️ This code was cancelled and no longer works.");
    private static readonly Localized Unavailable = new(
        "⚠️ الان به سایت وصل نمی‌شوم. چند دقیقه بعد دوباره بفرست؛ کدت سالم است.",
        "⚠️ Can't reach the website right now. Try again in a few minutes; your code is safe.");
    private static readonly Localized ProActivated = new(
        "💎 Pro این گروه فعال شد: {0} روز اضافه شد. اعتبار تا <b>{1}</b>.",
        "💎 Pro is active in this group: {0} days added. Valid until <b>{1}</b>.");
    private static readonly Localized PluginActivated = new(
        "🧩 پلاگین «{2}» فعال شد: {0} روز اضافه شد. اعتبار تا <b>{1}</b>.",
        "🧩 Plugin “{2}” is active: {0} days added. Valid until <b>{1}</b>.");
    private static readonly Localized AlreadyHere = new(
        "ℹ️ این کد قبلاً روی همین گروه فعال شده. اعتبار تا <b>{0}</b>.",
        "ℹ️ This code is already active in this group. Valid until <b>{0}</b>.");
    private static readonly Localized PluginMissing = new(
        "\n⚠️ این پلاگین هنوز روی این ربات نصب نیست؛ روزهایش ذخیره شد و با نصبش کار می‌کند.",
        "\n⚠️ This plugin is not installed on this bot yet; its days are saved and start working once it is.");

    public string PluginId => GroupSettingsPlugin.Id;

    public int Order => HandlerOrder.Commands;

    public async Task<HandlerResult> HandleAsync(GroupMessageContext context, CancellationToken ct)
    {
        if (context.Command is not { Name: "activate" } command) return HandlerResult.Continue;

        // Only admins buy and activate for a group. Members get no answer.
        if (!context.SenderIsAdmin) return HandlerResult.Continue;

        var lang = context.Group.Lang;
        var chatId = context.ChatId;
        var code = command.Args.Trim();

        if (code.Length == 0)
        {
            await ReplyAsync(context, Usage.Get(lang), ct);
            return HandlerResult.Stop;
        }

        if (!TryCount(chatId))
        {
            await ReplyAsync(context, TooMany.Get(lang), ct);
            return HandlerResult.Stop;
        }

        var userId = context.Message.From?.Id ?? 0;
        var answer = await shop.ActivateAsync(code, chatId, context.Group.Title, userId, ct);

        var text = answer switch
        {
            { Outcome: ActivationOutcome.Valid, Grant: { } grant } => await ApplyAsync(chatId, grant, userId, lang, ct),
            { Outcome: ActivationOutcome.NotFound } => NotFound.Get(lang),
            { Outcome: ActivationOutcome.UsedElsewhere } => UsedElsewhere.Get(lang),
            { Outcome: ActivationOutcome.Revoked } => Revoked.Get(lang),
            _ => Unavailable.Get(lang),
        };

        await ReplyAsync(context, text, ct);
        return HandlerResult.Stop;
    }

    private async Task<string> ApplyAsync(long chatId, LicenseGrant grant, long userId, string lang, CancellationToken ct)
    {
        var result = await redemptions.ApplyAsync(chatId, grant, userId, ct);
        var until = result.Until is { } u
            ? LocalTime.When(new DateTimeOffset(DateTime.SpecifyKind(u, DateTimeKind.Utc)), lang, clock)
            : "—";

        if (!result.Applied) return AlreadyHere.Format(lang, until);
        if (!grant.IsPlugin) return ProActivated.Format(lang, grant.Days, until);

        var installed = catalog.Contains(grant.PluginId!);
        var name = installed
            ? (lang == Languages.English ? catalog.Get(grant.PluginId!).NameEn : catalog.Get(grant.PluginId!).NameFa)
            : grant.PluginId!;
        var text = PluginActivated.Format(lang, grant.Days, until, Html.Escape(name));
        return installed ? text : text + PluginMissing.Get(lang);
    }

    private Task ReplyAsync(GroupMessageContext context, string html, CancellationToken ct) =>
        actions.SendTemporaryAsync(context.ChatId, html, AnswerLifetime, context.Message.MessageId, ct);

    private bool TryCount(long chatId)
    {
        var now = clock.GetUtcNow();
        var entry = _tries.AddOrUpdate(chatId,
            _ => (1, now),
            (_, old) => now - old.Since > TryWindow ? (1, now) : (old.Count + 1, old.Since));
        return entry.Count <= MaxTries;
    }
}
