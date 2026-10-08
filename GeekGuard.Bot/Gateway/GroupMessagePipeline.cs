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
    private readonly BotIdentity _me;
    private readonly ILogger<GroupMessagePipeline> _log;

    public GroupMessagePipeline(IEnumerable<IGroupMessageHandler> handlers, PluginCatalog catalog,
        PluginStateStore states, GroupDirectory groups, BotIdentity me, ILogger<GroupMessagePipeline> log)
    {
        // Resolving manifests here makes a handler with a wrong PluginId fail at startup, not on the first message.
        _handlers = handlers
            .OrderBy(h => h.Order)
            .Select(h => (h, catalog.Get(h.PluginId)))
            .ToArray();
        _states = states;
        _groups = groups;
        _me = me;
        _log = log;
    }

    public async Task RunAsync(Message message, CancellationToken ct)
    {
        var chat = message.Chat;
        var group = await _groups.GetOrAddAsync(chat.Id, chat.Title ?? "", message.From?.Id,
            Languages.FromTelegram(message.From?.LanguageCode), ct);

        var context = new GroupMessageContext
        {
            Message = message,
            Group = group,
            Command = CommandParser.Parse(message.Text, _me.Username),
        };

        foreach (var (handler, plugin) in _handlers)
        {
            if (!await _states.IsActiveAsync(chat.Id, plugin, ct)) continue;

            if (await handler.HandleAsync(context, ct) == HandlerResult.Stop)
            {
                _log.LogDebug("Message {MessageId} in {Chat} stopped by {Plugin}", message.MessageId, chat.Id, plugin.Id);
                return;
            }
        }
    }
}
