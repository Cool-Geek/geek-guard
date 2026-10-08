using Telegram.Bot.Types;

namespace GeekGuard.Bot.Gateway;

/// <summary>Who the bot is, as reported by Telegram at startup (getMe).</summary>
public sealed class BotIdentity
{
    public long Id { get; private set; }

    /// <summary>The bot's @username without the @, e.g. "geek_guard_bot".</summary>
    public string Username { get; private set; } = "";

    internal void Set(User me)
    {
        Id = me.Id;
        Username = me.Username ?? "";
    }
}
