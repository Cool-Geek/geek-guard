using CoolGeek.PersianText;
using GeekGuard.Core.Messaging;

namespace GeekGuard.Modules.Moderation;

public enum ModerationAction
{
    Warn,
    Unwarn,
    Mute,
    Unmute,
    Kick,
    Ban,
    Unban,
}

/// <summary>A moderation request, from either a slash command or a keyword reply.</summary>
/// <param name="IsSlashCommand">Slash commands answer non-admins with a hint; keywords from members are just chat.</param>
/// <param name="Duration">For mute and ban; null means until lifted.</param>
/// <param name="Reason">For warn; only slash commands carry one.</param>
public sealed record ModerationCommand(ModerationAction Action, bool IsSlashCommand, TimeSpan? Duration, string? Reason, bool InvalidDuration = false);

public static class ModerationCommandParser
{
    private static readonly Dictionary<string, ModerationAction> SlashCommands = new()
    {
        ["warn"] = ModerationAction.Warn,
        ["unwarn"] = ModerationAction.Unwarn,
        ["mute"] = ModerationAction.Mute,
        ["unmute"] = ModerationAction.Unmute,
        ["kick"] = ModerationAction.Kick,
        ["ban"] = ModerationAction.Ban,
        ["unban"] = ModerationAction.Unban,
    };

    // Keywords an admin can reply with instead of a slash command. Matched after normalization,
    // so "آزاد" and "ازاد", or "بن" typed with an Arabic keyboard, all work.
    private static readonly (string Keyword, ModerationAction Action)[] Keywords = new (string, ModerationAction)[]
        {
            ("اخطار", ModerationAction.Warn), ("warn", ModerationAction.Warn),
            ("حذف اخطار", ModerationAction.Unwarn), ("unwarn", ModerationAction.Unwarn),
            ("سکوت", ModerationAction.Mute), ("میوت", ModerationAction.Mute), ("mute", ModerationAction.Mute),
            ("رفع سکوت", ModerationAction.Unmute), ("آزاد", ModerationAction.Unmute), ("آنمیوت", ModerationAction.Unmute),
            ("unmute", ModerationAction.Unmute),
            ("اخراج", ModerationAction.Kick), ("کیک", ModerationAction.Kick), ("kick", ModerationAction.Kick),
            ("بن", ModerationAction.Ban), ("ban", ModerationAction.Ban),
            ("رفع بن", ModerationAction.Unban), ("آنبن", ModerationAction.Unban), ("unban", ModerationAction.Unban),
        }
        .Select(k => (PersianNormalizer.Normalize(k.Item1), k.Item2))
        // Longest first, so "رفع سکوت" wins over "سکوت".
        .OrderByDescending(k => k.Item1.Length)
        .ToArray();

    /// <summary>
    /// Reads a moderation command from a message. Keywords count only when the message is a reply and holds
    /// nothing but the keyword (plus a duration for mute and ban), so ordinary chat never triggers them.
    /// </summary>
    public static ModerationCommand? Parse(ParsedCommand? slash, NormalizedText text, bool isReply)
    {
        if (slash is not null)
        {
            if (!SlashCommands.TryGetValue(slash.Name, out var action)) return null;
            var args = slash.Args.Trim();
            return action switch
            {
                ModerationAction.Mute or ModerationAction.Ban when args.Length > 0 =>
                    Duration.Parse(args) is { } d
                        ? new ModerationCommand(action, true, d, null)
                        : new ModerationCommand(action, true, null, null, InvalidDuration: true),
                ModerationAction.Warn => new ModerationCommand(action, true, null, args.Length > 0 ? args : null),
                _ => new ModerationCommand(action, true, null, null),
            };
        }

        if (!isReply || text.IsEmpty) return null;

        foreach (var (keyword, action) in Keywords)
        {
            if (text.Value == keyword) return new ModerationCommand(action, false, null, null);

            if (action is ModerationAction.Mute or ModerationAction.Ban
                && text.Value.StartsWith(keyword + " ", StringComparison.Ordinal)
                && Duration.Parse(text.Value[(keyword.Length + 1)..]) is { } duration)
            {
                return new ModerationCommand(action, false, duration, null);
            }
        }
        return null;
    }
}
