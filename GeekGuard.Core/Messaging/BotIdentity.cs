using Telegram.Bot.Types;

namespace GeekGuard.Core.Messaging;

/// <summary>Who the bot is, as reported by Telegram at startup (getMe).</summary>
public sealed class BotIdentity
{
    public long Id { get; private set; }

    /// <summary>The bot's @username without the @, e.g. "geek_guard_bot".</summary>
    public string Username { get; private set; } = "";

    /// <summary>Called once by the host after getMe succeeds.</summary>
    public void Initialize(User me)
    {
        Id = me.Id;
        Username = me.Username ?? "";
    }
}
