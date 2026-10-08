using GeekGuard.Bot.Configuration;
using Microsoft.Extensions.Options;

var builder = Host.CreateApplicationBuilder(args);

// Settings come from appsettings.json, then User Secrets (Development only), then environment variables.
// On the server, set secrets as environment variables, e.g. Telegram__BotToken.
builder.Services
    .AddOptions<TelegramOptions>()
    .Bind(builder.Configuration.GetSection(TelegramOptions.SectionName))
    .Validate(o => !string.IsNullOrWhiteSpace(o.BotToken),
        "Telegram:BotToken is missing. Set it with User Secrets (see README).")
    .ValidateOnStart();

var app = builder.Build();

var log = app.Services.GetRequiredService<ILoggerFactory>().CreateLogger("GeekGuard");
var telegram = app.Services.GetRequiredService<IOptions<TelegramOptions>>().Value;
log.LogInformation("Geek Guard is starting in {Environment} mode", builder.Environment.EnvironmentName);
log.LogInformation("Bot token loaded: {Token}", TelegramOptions.Mask(telegram.BotToken));

await app.RunAsync();
