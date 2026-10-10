using GeekGuard.Core.Messaging;
using GeekGuard.Core.Panel;
using Telegram.Bot.Types.ReplyMarkups;

namespace GeekGuard.Modules.Locks;

/// <summary>Panel: every lock as a button that flips it.</summary>
public sealed class LocksSection(PluginStateStore states) : IPanelSection
{
    private static readonly Localized Text = new(
        "🔒 <b>قفل‌ها</b>\n\nروی هر مورد بزن تا قفل یا باز شود. 🔒 یعنی اعضا نمی‌توانند آن را بفرستند؛ ادمین‌ها همیشه آزادند.\n\nاخطار به فرستنده: <b>{0}</b>",
        "🔒 <b>Locks</b>\n\nTap an item to lock or unlock it. 🔒 means members can't send it; admins are always free.\n\nWarn the sender: <b>{0}</b>");

    public string Id => "locks";

    public string PluginId => LocksPlugin.Id;

    public int Order => 50;

    public Localized Title { get; } = new("🔒 قفل‌ها", "🔒 Locks");

    public bool HasPluginSwitch => false;

    public async Task<PanelView> ShowAsync(PanelContext context, CancellationToken ct)
    {
        var s = await states.GetSettingsAsync<LocksSettings>(context.ChatId, LocksPlugin.Id, ct);
        var lang = context.Lang;
        var fa = lang != Languages.English;

        var rows = new List<InlineKeyboardButton[]> { PanelButtons.Header(fa ? "🔒 قفل = قرمز · 🔓 آزاد" : "🔒 locked = red · 🔓 allowed") };
        rows.AddRange(LockTypes.All
            .Select(t => s.IsLocked(t)
                ? PanelButtons.Danger("🔒 " + LockTypes.Name(t, lang), context, Id, "t", t.ToString())
                : PanelButtons.Action("🔓 " + LockTypes.Name(t, lang), context, Id, "t", t.ToString()))
            .Chunk(2));
        rows.Add(PanelButtons.Header(fa ? "⚡ همه با هم" : "⚡ All at once"));
        rows.Add(new[]
        {
            PanelButtons.Action(fa ? "🔒 قفل همه" : "🔒 Lock all", context, Id, "all"),
            PanelButtons.Action(fa ? "🔓 باز کردن همه" : "🔓 Unlock all", context, Id, "none"),
        });
        rows.Add(PanelButtons.Header(fa ? "⚠️ فرستنده‌ی محتوای قفل‌شده" : "⚠️ Sender of locked content"));
        rows.Add(new[] { PanelButtons.Toggle(fa ? "اخطار بگیرد" : "Gets a warning", s.WarnOnViolation, context, Id, "warn") });
        rows.Add(new[] { PanelButtons.BackToGroup(context) });

        return new PanelView(context.T(Text).Replace("{0}", s.WarnOnViolation ? "✅" : "❌"), new InlineKeyboardMarkup(rows));
    }

    public async Task<string?> HandleAsync(PanelContext context, string[] args, CancellationToken ct)
    {
        var s = await states.GetSettingsAsync<LocksSettings>(context.ChatId, LocksPlugin.Id, ct);
        var locked = s.Locked.ToList();
        var warn = s.WarnOnViolation;

        switch (args[0])
        {
            case "t" when args.Length > 1 && Enum.TryParse<LockType>(args[1], out var type) && Enum.IsDefined(type):
                if (!locked.Remove(type)) locked.Add(type);
                break;
            case "all":
                locked = LockTypes.All.ToList();
                break;
            case "none":
                locked = [];
                break;
            case "warn":
                warn = !warn;
                break;
            default:
                return null;
        }

        await states.SaveSettingsAsync(context.ChatId, LocksPlugin.Id, new LocksSettings { Locked = locked, WarnOnViolation = warn }, ct);
        return null;
    }
}
