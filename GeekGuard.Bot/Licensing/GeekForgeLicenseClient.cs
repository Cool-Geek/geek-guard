using System.Net;
using System.Net.Http.Json;
using GeekGuard.Core.Licensing;
using Microsoft.Extensions.Options;

namespace GeekGuard.Bot.Licensing;

/// <summary>Calls the shop's POST /api/bot/licenses/activate.</summary>
public sealed class GeekForgeLicenseClient(HttpClient http, IOptions<GeekForgeOptions> options, ILogger<GeekForgeLicenseClient> log)
    : ILicenseActivator
{
    private sealed record Request(string Code, long ChatId, string ChatTitle, long TelegramUserId);

    private sealed record Response(bool Ok, string? Code, string? Kind, string? PluginId, int Days, string? Product, string? Error);

    public async Task<ActivationAnswer> ActivateAsync(string code, long chatId, string chatTitle, long telegramUserId, CancellationToken ct = default)
    {
        var settings = options.Value;
        if (!settings.IsConfigured)
        {
            log.LogWarning("/activate used but GeekForge:BaseUrl / GeekForge:ApiKey are not set");
            return new ActivationAnswer(ActivationOutcome.Unavailable);
        }

        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, new Uri(new Uri(settings.BaseUrl), "/api/bot/licenses/activate"))
            {
                Content = JsonContent.Create(new Request(code, chatId, chatTitle, telegramUserId)),
            };
            request.Headers.Add("X-Api-Key", settings.ApiKey.Trim());

            using var response = await http.SendAsync(request, ct);
            switch (response.StatusCode)
            {
                case HttpStatusCode.OK:
                    var body = await response.Content.ReadFromJsonAsync<Response>(ct);
                    if (body is not { Ok: true, Code: { } accepted, Days: > 0 })
                        break;
                    var isPlugin = body.Kind == "plugin";
                    if (isPlugin && string.IsNullOrWhiteSpace(body.PluginId))
                        break;
                    return new ActivationAnswer(ActivationOutcome.Valid,
                        new LicenseGrant(accepted, isPlugin, isPlugin ? body.PluginId : null, body.Days, body.Product));

                case HttpStatusCode.NotFound:
                    return new ActivationAnswer(ActivationOutcome.NotFound);
                case HttpStatusCode.Conflict:
                    return new ActivationAnswer(ActivationOutcome.UsedElsewhere);
                case HttpStatusCode.Gone:
                    return new ActivationAnswer(ActivationOutcome.Revoked);
            }

            log.LogWarning("Shop answered /activate with {Status}", (int)response.StatusCode);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or System.Text.Json.JsonException
                                       && !ct.IsCancellationRequested)
        {
            log.LogWarning("Could not reach the shop for /activate: {Message}", ex.Message);
        }

        return new ActivationAnswer(ActivationOutcome.Unavailable);
    }
}

public static class LicensingServiceCollectionExtensions
{
    /// <summary>Registers the shop client used by /activate.</summary>
    public static IServiceCollection AddGeekForgeLicensing(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<GeekForgeOptions>().Bind(configuration.GetSection(GeekForgeOptions.SectionName));
        // One long-lived client (handlers are singletons); recycling connections picks up DNS changes.
        services.AddSingleton<ILicenseActivator>(sp => new GeekForgeLicenseClient(
            new HttpClient(new SocketsHttpHandler { PooledConnectionLifetime = TimeSpan.FromMinutes(5) })
            {
                Timeout = TimeSpan.FromSeconds(15),
            },
            sp.GetRequiredService<IOptions<GeekForgeOptions>>(),
            sp.GetRequiredService<ILogger<GeekForgeLicenseClient>>()));
        return services;
    }
}
