using GeekGuard.Core.Groups;
using GeekGuard.Core.Messaging;
using GeekGuard.Core.Pipeline;
using GeekGuard.Core.Plugins;
using Telegram.Bot.Types;

namespace GeekGuard.Bot.Gateway;

/// <summary>
/// Passes each group message through the handlers of the plugins active in that group, in order,
/// until one of them stops it.
/// </summary>
public sealed class GroupMessagePipeline
{
    private readonly (IGroupMessageHandler Handler, PluginManifest Plugin)[] _handlers;
    private readonly PluginStateStore _states;
    private readonly GroupDirectory _groups;
    private readonly GroupAdmins _admins;
    private readonly MemberDirectory _members;
    private readonly BotIdentity _me;
    private readonly ILogger<GroupMessagePipeline> _log;

    public GroupMessagePipeline(IEnumerable<IGroupMessageHandler> handlers, PluginCatalog catalog,
        PluginStateStore states, GroupDirectory groups, GroupAdmins admins, MemberDirectory members, BotIdentity me, ILogger<GroupMessagePipeline> log)
    {
        // Resolving manifests here makes a handler with a wrong PluginId fail at startup, not on the first message.
        _handlers = handlers
            .OrderBy(h => h.Order)
            .Select(h => (h, catalog.Get(h.PluginId)))
            .ToArray();
        _states = states;
        _groups = groups;
        _admins = admins;
        _members = members;
        _me = me;
        _log = log;
    }

    /// <param name="isEdit">The message was edited: only content filters look at it again.</param>
    public async Task RunAsync(Message message, CancellationToken ct, bool isEdit = false)
    {
        var chat = message.Chat;
        var group = await _groups.GetOrAddAsync(chat.Id, chat.Title ?? "", message.From?.Id,
            Languages.ForNewGroup(chat.Title, message.From?.LanguageCode), ct);

        await RememberPeopleAsync(message, ct);

        var context = new GroupMessageContext
        {
            Message = message,
            Group = group,
            // An edit never runs a command: fixing a typo in an old "/ban" must not ban anyone again.
            Command = isEdit ? null : CommandParser.Parse(message.Text, _me.Username),
            IsEdit = isEdit,
            SenderIsAdmin = await IsFromAdminAsync(message, ct),
        };

        foreach (var (handler, plugin) in _handlers)
        {
            if (isEdit && !HandlerOrder.SeesEdits(handler.Order)) continue;
            if (!await _states.IsActiveAsync(chat.Id, plugin, ct)) continue;

            if (await handler.HandleAsync(context, ct) == HandlerResult.Stop)
            {
                _log.LogDebug("Message {MessageId} in {Chat} stopped by {Plugin}", message.MessageId, chat.Id, plugin.Id);
                return;
            }
        }
    }

    /// <summary>
    /// Notes who appears in the message (sender, the member replied to, people who joined or were tapped in a mention),
    /// so admins can later name them by @username. Failing to note them never blocks the message.
    /// </summary>
    private async Task RememberPeopleAsync(Message message, CancellationToken ct)
    {
        var people = new List<User>();
        if (message is { SenderChat: null, From: { } from }) people.Add(from);
        if (message.ReplyToMessage is { SenderChat: null, From: { } replied }) people.Add(replied);
        if (message.NewChatMembers is { } joined) people.AddRange(joined);
        people.AddRange((message.Entities ?? []).Where(e => e.User is not null).Select(e => e.User!));

        try
        {
            foreach (var user in people.DistinctBy(u => u.Id)) await _members.SeeAsync(message.Chat.Id, user, ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _log.LogWarning(ex, "Could not record members of {Chat}", message.Chat.Id);
        }
    }

    /// <summary>
    /// Admins, anonymous admins (who post as the group) and the group's linked channel are trusted.
    /// A member posting as one of their own channels is not.
    /// </summary>
    private async Task<bool> IsFromAdminAsync(Message message, CancellationToken ct)
    {
        if (message.IsAutomaticForward) return true;
        if (message.SenderChat is { } senderChat) return senderChat.Id == message.Chat.Id;
        return message.From is { } from && await _admins.IsAdminAsync(message.Chat.Id, from.Id, ct);
    }
}
