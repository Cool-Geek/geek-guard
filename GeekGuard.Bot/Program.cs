using GeekGuard.Bot.Configuration;
using GeekGuard.Bot.Gateway;
using GeekGuard.Bot.Plugins;
using GeekGuard.Core;
using GeekGuard.Core.Data.Migrations;
using GeekGuard.Modules.Diagnostics;

var builder = Host.CreateApplicationBuilder(args);

// Settings come from appsettings.json, then User Secrets (Development only), then environment variables.
// On the server, set secrets as environment variables, e.g. Telegram__BotToken.
builder.Services
    .AddOptions<TelegramOptions>()
    .Bind(builder.Configuration.GetSection(TelegramOptions.SectionName))
    .Validate(o => !string.IsNullOrWhiteSpace(o.BotToken),
        "Telegram:BotToken is missing. Set it with User Secrets (see README).")
    .Validate(o => string.IsNullOrWhiteSpace(o.BotToken) || TelegramOptions.LooksValid(o.BotToken),
        "Telegram:BotToken does not look like a BotFather token (expected 123456789:AA...). " +
        "Check User Secrets for extra spaces, quotes or placeholder text.")
    .ValidateOnStart();

builder.Services.AddGeekGuardCore(builder.Configuration);
builder.Services.AddTelegramBot();

// Plugins register services, so they load before the host is built; a console logger reports what loaded.
using (var bootLog = LoggerFactory.Create(logging => logging.AddConsole()))
{
    builder.Services.AddGeekGuardPlugins(builder.Configuration, bootLog.CreateLogger("Plugins"),
        typeof(DiagnosticsPlugin).Assembly);
}

var app = builder.Build();

// The schema must be current before the first update is handled.
await app.Services.GetRequiredService<MigrationRunner>().RunAsync();

await app.RunAsync();
