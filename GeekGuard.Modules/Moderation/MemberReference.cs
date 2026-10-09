using System.Text.RegularExpressions;
using CoolGeek.PersianText;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;

namespace GeekGuard.Modules.Moderation;

/// <summary>How an admin named a member without replying: by numeric id, by @username, or by a tapped mention.</summary>
public abstract record MemberReference
{
    /// <summary>«بن 123456789». Works for anyone, even people the bot has never seen.</summary>
    public sealed record ById(long UserId) : MemberReference;

    /// <summary>«بن @ali». Works for people the bot has seen in the group.</summary>
    public sealed record ByUsername(string Username) : MemberReference;

    /// <summary>A mention picked from Telegram's @ list for someone without a username; it carries their id.</summary>
    public sealed record ByMention(User User) : MemberReference;
}

public static partial class MemberReferenceParser
{
    [GeneratedRegex("^@[A-Za-z][A-Za-z0-9_]{4,31}$")]
    private static partial Regex Username();

    // Telegram ids have at least six digits, so durations like «سکوت 30 دقیقه» are never mistaken for one.
    [GeneratedRegex("^[0-9]{6,15}$")]
    private static partial Regex UserId();

    /// <summary>
    /// Finds the member named in the message and returns the text without them, so «سکوت @ali 2 ساعت» can be read
    /// as «سکوت 2 ساعت». Returns null when no member is named.
    /// </summary>
    public static (MemberReference Reference, string RemainingText)? Extract(Message message)
    {
        var text = message.Text;
        if (string.IsNullOrEmpty(text)) return null;

        if ((message.Entities ?? []).FirstOrDefault(e => e.Type == MessageEntityType.TextMention && e.User is not null) is { } mention
            && mention.Offset + mention.Length <= text.Length)
        {
            return (new MemberReference.ByMention(mention.User!), Tidy(text.Remove(mention.Offset, mention.Length)));
        }

        var tokens = text.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
        for (var i = 0; i < tokens.Length; i++)
        {
            var token = tokens[i];
            if (token.StartsWith('/')) continue; // "/ban@geek_guard_bot" is the command, not a member

            MemberReference? reference = null;
            if (Username().IsMatch(token)) reference = new MemberReference.ByUsername(token[1..]);
            else if (PersianNormalizer.Normalize(token) is var digits && UserId().IsMatch(digits))
                reference = new MemberReference.ById(long.Parse(digits));

            if (reference is not null)
                return (reference, string.Join(' ', tokens.Where((_, index) => index != i)));
        }
        return null;
    }

    private static string Tidy(string text) => string.Join(' ', text.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
}
