using GeekGuard.Core.Commands;
using GeekGuard.Core.Messaging;
using Telegram.Bot;
using Telegram.Bot.Types;

namespace GeekGuard.Bot.Gateway;

/// <summary>
/// Publishes Telegram's "/" command menu with scopes: group admins see the admin commands in every group, the
/// private chat shows the panel and guide, and ordinary members see no menu at all in groups.
/// Each list goes out in Persian (for Persian apps) and English (everyone else).
/// </summary>
public sealed class CommandMenuPublisher(ITelegramBotClient bot, IEnumerable<BotCommandEntry> entries, ILogger<CommandMenuPublisher> log)
{
    /// <summary>The host's own private-chat commands; plugins add the rest.</summary>
    private static readonly BotCommandEntry[] HostCommands =
    [
        new("start", new("⚙️ پنل تنظیمات گروه‌ها", "⚙️ Settings panel for your groups"), CommandAudience.PrivateChat, 10),
        new("help", new("📖 راهنمای کامل دستورها", "📖 Full guide to every command"), CommandAudience.PrivateChat, 20),
        new("cancel", new("❌ لغو کاری که در پنل نیمه‌کاره مانده", "❌ Cancel what you started in the panel"), CommandAudience.PrivateChat, 30),
    ];

    public async Task PublishAsync(CancellationToken ct)
    {
        var all = HostCommands.Concat(entries).ToList();
        try
        {
            // Members: nothing. Without commands for these scopes Telegram shows no menu in groups.
            await bot.DeleteMyCommands(BotCommandScope.Default(), cancellationToken: ct);
            await bot.DeleteMyCommands(BotCommandScope.AllGroupChats(), cancellationToken: ct);

            await PublishScopeAsync(BotCommandScope.AllChatAdministrators(), all, CommandAudience.GroupAdmins, ct);
            await PublishScopeAsync(BotCommandScope.AllPrivateChats(), all, CommandAudience.PrivateChat, ct);
            log.LogInformation("Command menu published ({Admin} admin, {Private} private commands)",
                all.Count(c => c.Audience == CommandAudience.GroupAdmins), all.Count(c => c.Audience == CommandAudience.PrivateChat));
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // The bot works without the menu; it is only a convenience.
            log.LogWarning("Could not publish the command menu: {Message}", ex.Message);
        }
    }

    private async Task PublishScopeAsync(BotCommandScope scope, List<BotCommandEntry> all, CommandAudience audience, CancellationToken ct)
    {
        var chosen = all.Where(c => c.Audience == audience)
            .OrderBy(c => c.Order)
            .DistinctBy(c => c.Command)
            .Take(100) // Telegram's limit
            .ToList();

        BotCommand[] Commands(string lang) =>
            chosen.Select(c => new BotCommand { Command = c.Command, Description = c.Description.Raw(lang) }).ToArray();

        // English is the fallback for every app language; Persian apps get the Persian list.
        await bot.SetMyCommands(Commands(Languages.English), scope, cancellationToken: ct);
        await bot.SetMyCommands(Commands(Languages.Persian), scope, languageCode: "fa", cancellationToken: ct);
    }
}
