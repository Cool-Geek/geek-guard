using GeekGuard.Core.Messaging;

namespace GeekGuard.Core.Help;

/// <summary>
/// One page of the user guide in the bot's private chat. Each plugin adds the pages for its own features with
/// <c>services.AddHelpTopic(...)</c>, so a Pro plugin brings its guide along and gets the 💎 badge automatically.
/// </summary>
/// <param name="Id">Short id for button data, e.g. "mod". Letters only, unique.</param>
/// <param name="PluginId">The plugin the page describes; its tier (Free / Pro) is shown on the page.</param>
/// <param name="Order">Position in the guide's menu.</param>
/// <param name="Title">Button label, with an emoji, e.g. «⚠️ اخطار و مجازات».</param>
/// <param name="Body">The page, in Telegram HTML.</param>
public sealed record HelpTopic(string Id, string PluginId, int Order, Localized Title, Localized Body);
