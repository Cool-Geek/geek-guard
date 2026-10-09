using CoolGeek.PersianText;
using GeekGuard.Core.Groups;
using GeekGuard.Core.Messaging;
using Telegram.Bot.Types;

namespace GeekGuard.Core.Pipeline;

/// <summary>What a handler wants to happen next.</summary>
public enum HandlerResult
{
    /// <summary>Let the next handler see the message.</summary>
    Continue = 0,

    /// <summary>The message is dealt with (answered, deleted…); later handlers are skipped.</summary>
    Stop = 1,
}

/// <summary>Everything a handler needs to know about one group message.</summary>
public sealed class GroupMessageContext
{
    public required Message Message { get; init; }

    public required GroupInfo Group { get; init; }

    /// <summary>The slash command in the message, if it is addressed to this bot.</summary>
    public ParsedCommand? Command { get; init; }

    /// <summary>
    /// True when the sender is a group admin, an anonymous admin, or the group's linked channel.
    /// Filters and locks leave these messages alone.
    /// </summary>
    public required bool SenderIsAdmin { get; init; }

    /// <summary>True when an admin posted anonymously, i.e. as the group itself.</summary>
    public bool IsAnonymousAdmin => Message.SenderChat?.Id == ChatId;

    public long ChatId => Message.Chat.Id;

    /// <summary>Text or caption of the message.</summary>
    public string Text => Message.Text ?? Message.Caption ?? "";

    private NormalizedText? _normalized;

    /// <summary>The text normalized once for every filter that needs it.</summary>
    public NormalizedText NormalizedText => _normalized ??= NormalizedText.From(Text);
}

/// <summary>
/// Sees every message in groups where its plugin is active, in ascending <see cref="Order"/>.
/// Register with <c>services.AddGroupMessageHandler&lt;T&gt;()</c>.
/// </summary>
public interface IGroupMessageHandler
{
    /// <summary>The plugin this handler belongs to. It only runs where that plugin is active.</summary>
    string PluginId { get; }

    /// <summary>Position in the pipeline; see <see cref="HandlerOrder"/>.</summary>
    int Order { get; }

    Task<HandlerResult> HandleAsync(GroupMessageContext context, CancellationToken ct);
}

/// <summary>
/// Pipeline positions. Gaps leave room for plugins to slot in between.
/// Commands go first so an admin's /unmute is never swallowed by a filter.
/// </summary>
public static class HandlerOrder
{
    public const int Commands = 100;
    public const int Membership = 200;
    public const int Flood = 300;
    public const int ContentFilters = 400;
    public const int Late = 900;
}
