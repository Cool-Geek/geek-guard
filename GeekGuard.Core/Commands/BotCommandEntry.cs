using GeekGuard.Core.Messaging;

namespace GeekGuard.Core.Commands;

/// <summary>Who sees a command in Telegram's "/" menu.</summary>
public enum CommandAudience
{
    /// <summary>Group admins only, in every group. Members never see it.</summary>
    GroupAdmins,

    /// <summary>The bot's private chat.</summary>
    PrivateChat,
}

/// <summary>
/// One line of the "/" command menu. Plugins add theirs with <c>services.AddBotCommand(...)</c>; the bot publishes
/// the whole menu to Telegram at startup, in Persian and English (by the user's app language).
/// </summary>
/// <param name="Command">Without the slash: lowercase letters, digits and _, at most 32 characters.</param>
/// <param name="Description">Shown next to the command (Telegram allows up to 256 characters).</param>
/// <param name="Order">Position in the menu: lower comes first.</param>
public sealed record BotCommandEntry(string Command, Localized Description, CommandAudience Audience, int Order);
