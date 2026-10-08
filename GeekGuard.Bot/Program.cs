using GeekGuard.Bot.Configuration;
using GeekGuard.Bot.Gateway;

var builder = Host.CreateApplicationBuilder(args);

// Settings come from appsettings.json, then User Secrets (Development only), then environment variables.
// On the server, set secrets as environment variables, e.g. Telegram__BotToken.
builder.Services
    .AddOptions<TelegramOptions>()
    .Bind(builder.Configuration.GetSection(TelegramOptions.SectionName))
    .Validate(o => !string.IsNullOrWhiteSpace(o.BotToken),
        "Telegram:BotToken is missing. Set it with User Secrets (see README).")
    .ValidateOnStart();

builder.Services.AddTelegramBot();

var app = builder.Build();
await app.RunAsync();
