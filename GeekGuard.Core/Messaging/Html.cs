using Telegram.Bot.Types;

namespace GeekGuard.Core.Messaging;

/// <summary>Helpers for messages sent with Telegram's HTML parse mode.</summary>
public static class Html
{
    /// <summary>Escapes the three characters Telegram's HTML mode treats specially.</summary>
    public static string Escape(string? text) =>
        string.IsNullOrEmpty(text) ? "" : text.Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;");

    /// <summary>"First Last", falling back to @username or the numeric id.</summary>
    public static string DisplayName(User user)
    {
        var name = string.IsNullOrWhiteSpace(user.LastName) ? user.FirstName : $"{user.FirstName} {user.LastName}";
        return string.IsNullOrWhiteSpace(name) ? user.Username ?? user.Id.ToString() : name.Trim();
    }

    /// <summary>A clickable mention that works even for users without a username.</summary>
    public static string Mention(User user) =>
        $"<a href=\"tg://user?id={user.Id}\">{Escape(Truncate(DisplayName(user), 40))}</a>";

    /// <summary>A mention followed by the numeric id, so admins can undo an action later with «رفع بن 123456789».</summary>
    public static string MentionWithId(User user) => $"{Mention(user)} (<code>{user.Id}</code>)";

    public static string Truncate(string text, int max) => text.Length <= max ? text : text[..(max - 1)] + "…";
}
