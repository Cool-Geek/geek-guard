using System.Net;
using GeekGuard.Bot.Configuration;
using Microsoft.Extensions.Options;
using Telegram.Bot;

namespace GeekGuard.Bot.Gateway;

public static class TelegramServiceCollectionExtensions
{
    /// <summary>
    /// Registers the Telegram client, update routing and the background service that receives updates.
    /// </summary>
    public static IServiceCollection AddTelegramBot(this IServiceCollection services)
    {
        services.AddSingleton<ITelegramBotClient>(sp =>
        {
            var options = sp.GetRequiredService<IOptions<TelegramOptions>>().Value;
            return new TelegramBotClient(options.BotToken.Trim(), CreateHttpClient(options.Proxy));
        });

        services.AddSingleton<UpdateRouter>();
        services.AddSingleton<GroupMessagePipeline>();
        services.AddHostedService<UpdateReceiver>();
        return services;
    }

    private static HttpClient CreateHttpClient(string? proxy)
    {
        var handler = new SocketsHttpHandler
        {
            // Recycle connections now and then so DNS changes are picked up on long-running servers.
            PooledConnectionLifetime = TimeSpan.FromMinutes(5),
        };

        if (!string.IsNullOrWhiteSpace(proxy))
        {
            // .NET understands http://, https:// and socks5:// proxy URLs.
            handler.Proxy = new WebProxy(proxy);
            handler.UseProxy = true;
        }

        // Long polling keeps a request open for up to a minute, so the timeout must be longer than that.
        return new HttpClient(handler) { Timeout = TimeSpan.FromSeconds(100) };
    }
}
