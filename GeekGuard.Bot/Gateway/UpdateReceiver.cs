using GeekGuard.Bot.Configuration;
using GeekGuard.Core.Messaging;
using Microsoft.Extensions.Options;
using Telegram.Bot;
using Telegram.Bot.Polling;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;

namespace GeekGuard.Bot.Gateway;

/// <summary>
/// Connects to Telegram and long-polls for updates (no domain or HTTPS needed),
/// handing each one to <see cref="UpdateRouter"/>.
/// </summary>
public sealed class UpdateReceiver(
    ITelegramBotClient bot,
    BotIdentity identity,
    UpdateRouter router,
    IOptions<TelegramOptions> options,
    ILogger<UpdateReceiver> log) : BackgroundService
{
    /// <summary>How many updates may be handled at the same time.</summary>
    private const int MaxParallelUpdates = 32;

    private readonly SemaphoreSlim _slots = new(MaxParallelUpdates);

    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        if (!string.IsNullOrWhiteSpace(options.Value.Proxy))
            log.LogInformation("Using proxy {Proxy}", options.Value.Proxy);

        var me = await ConnectAsync(ct);
        identity.Initialize(me);
        log.LogInformation("Connected as @{Username} (id {Id})", identity.Username, identity.Id);

        var receiverOptions = new ReceiverOptions
        {
            AllowedUpdates = [UpdateType.Message, UpdateType.EditedMessage, UpdateType.CallbackQuery,
                              UpdateType.MyChatMember, UpdateType.ChatMember],
        };

        await bot.ReceiveAsync(DispatchAsync, HandlePollingErrorAsync, receiverOptions, ct);
    }

    /// <summary>Calls getMe until Telegram answers, so a slow network or proxy at boot is not fatal.</summary>
    private async Task<User> ConnectAsync(CancellationToken ct)
    {
        for (var attempt = 1; ; attempt++)
        {
            try
            {
                return await bot.GetMe(ct);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                var wait = TimeSpan.FromSeconds(Math.Min(60, 5 * attempt));
                log.LogWarning("Cannot reach Telegram (attempt {Attempt}): {Message}. Retrying in {Seconds}s",
                    attempt, ex.Message, wait.TotalSeconds);
                await Task.Delay(wait, ct);
            }
        }
    }

    /// <summary>
    /// Runs each update on its own task so one slow chat cannot hold up the others.
    /// Waiting for a free slot also slows polling down when the bot is overloaded.
    /// </summary>
    private async Task DispatchAsync(ITelegramBotClient client, Update update, CancellationToken ct)
    {
        await _slots.WaitAsync(ct);
        _ = Task.Run(async () =>
        {
            try
            {
                await router.RouteAsync(update, ct);
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                // Shutting down.
            }
            catch (Exception ex)
            {
                log.LogError(ex, "Failed to handle update {UpdateId} ({Type})", update.Id, update.Type);
            }
            finally
            {
                _slots.Release();
            }
        }, CancellationToken.None);
    }

    private async Task HandlePollingErrorAsync(ITelegramBotClient client, Exception ex, CancellationToken ct)
    {
        log.LogWarning("Polling error: {Message}", ex.Message);
        // Back off briefly so a network outage does not turn into a tight retry loop.
        await Task.Delay(TimeSpan.FromSeconds(3), ct);
    }
}
