namespace GeekGuard.Core.Messaging;

/// <summary>A slash command addressed to this bot: "/mute@geek_guard_bot 2h" → Name "mute", Args "2h".</summary>
public sealed record ParsedCommand(string Name, string Args);

public static class CommandParser
{
    /// <summary>
    /// Parses a slash command. Returns null for plain text, and for commands addressed to another bot
    /// ("/start@other_bot"), so several bots can share a group without answering each other's commands.
    /// </summary>
    public static ParsedCommand? Parse(string? text, string botUsername)
    {
        if (string.IsNullOrEmpty(text) || text[0] != '/' || text.Length < 2) return null;

        var space = text.IndexOfAny([' ', '\n']);
        var head = space < 0 ? text[1..] : text[1..space];
        var args = space < 0 ? "" : text[(space + 1)..].Trim();

        var at = head.IndexOf('@');
        if (at >= 0)
        {
            var target = head[(at + 1)..];
            if (!target.Equals(botUsername, StringComparison.OrdinalIgnoreCase)) return null;
            head = head[..at];
        }

        return head.Length == 0 ? null : new ParsedCommand(head.ToLowerInvariant(), args);
    }
}
