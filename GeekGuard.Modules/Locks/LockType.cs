using CoolGeek.PersianText;
using GeekGuard.Core.Messaging;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;

namespace GeekGuard.Modules.Locks;

/// <summary>Kinds of content an admin can lock. A locked kind is removed when a member posts it.</summary>
public enum LockType
{
    Forward,
    Username,
    Photo,
    Video,
    Sticker,
    Gif,
    File,
    Voice,
    Poll,

    /// <summary>Bots added by members are removed at once.</summary>
    Bots,

    /// <summary>"X joined" / "X left" notices are deleted.</summary>
    Service,
}

/// <summary>Names admins type for each lock, and how to recognise each kind of content.</summary>
public static class LockTypes
{
    private static readonly Dictionary<string, LockType> ByName = new Dictionary<string, LockType>
        {
            ["forward"] = LockType.Forward, ["فوروارد"] = LockType.Forward,
            ["username"] = LockType.Username, ["mention"] = LockType.Username, ["آیدی"] = LockType.Username, ["یوزرنیم"] = LockType.Username,
            ["photo"] = LockType.Photo, ["عکس"] = LockType.Photo,
            ["video"] = LockType.Video, ["ویدیو"] = LockType.Video, ["فیلم"] = LockType.Video,
            ["sticker"] = LockType.Sticker, ["استیکر"] = LockType.Sticker,
            ["gif"] = LockType.Gif, ["گیف"] = LockType.Gif,
            ["file"] = LockType.File, ["document"] = LockType.File, ["فایل"] = LockType.File,
            ["voice"] = LockType.Voice, ["audio"] = LockType.Voice, ["ویس"] = LockType.Voice, ["صدا"] = LockType.Voice,
            ["poll"] = LockType.Poll, ["نظرسنجی"] = LockType.Poll,
            ["bots"] = LockType.Bots, ["bot"] = LockType.Bots, ["ربات"] = LockType.Bots,
            ["service"] = LockType.Service, ["join"] = LockType.Service, ["ورود"] = LockType.Service,
        }
        // Keys are matched after normalization, so «آیدی» and «ايدي» are the same.
        .ToDictionary(p => PersianNormalizer.Normalize(p.Key), p => p.Value);

    public static LockType? Parse(string name) => ByName.TryGetValue(PersianNormalizer.Normalize(name), out var type) ? type : null;

    /// <summary>Names that mean every lock at once: «قفل همه», /lock all.</summary>
    private static readonly HashSet<string> AllNames =
        new[] { "all", "everything", "همه", "همه‌چی", "همه‌چیز", "همشون", "همه‌شون" }.Select(PersianNormalizer.Normalize).ToHashSet();

    /// <summary>True for "all" / «همه».</summary>
    public static bool IsAll(string name) => AllNames.Contains(PersianNormalizer.Normalize(name));

    /// <summary>The kinds a typed name stands for: one kind, every kind for «همه», or null if unknown.</summary>
    public static IReadOnlyList<LockType>? Expand(string name) =>
        IsAll(name) ? All : Parse(name) is { } type ? new[] { type } : null;

    /// <summary>All kinds in display order.</summary>
    public static IReadOnlyList<LockType> All { get; } = Enum.GetValues<LockType>();

    public static string Name(LockType type, string lang)
    {
        var fa = lang != Languages.English;
        return type switch
        {
            LockType.Forward => fa ? "فوروارد" : "forwards",
            LockType.Username => fa ? "آیدی (@)" : "@usernames",
            LockType.Photo => fa ? "عکس" : "photos",
            LockType.Video => fa ? "ویدیو" : "videos",
            LockType.Sticker => fa ? "استیکر" : "stickers",
            LockType.Gif => fa ? "گیف" : "GIFs",
            LockType.File => fa ? "فایل" : "files",
            LockType.Voice => fa ? "ویس" : "voice",
            LockType.Poll => fa ? "نظرسنجی" : "polls",
            LockType.Bots => fa ? "ورود ربات" : "bots joining",
            _ => fa ? "پیام ورود و خروج" : "join/leave notices",
        };
    }

    /// <summary>The single word admins type for a lock, e.g. in «قفل عکس».</summary>
    public static string Keyword(LockType type, string lang)
    {
        var fa = lang != Languages.English;
        return type switch
        {
            LockType.Forward => fa ? "فوروارد" : "forward",
            LockType.Username => fa ? "آیدی" : "username",
            LockType.Photo => fa ? "عکس" : "photo",
            LockType.Video => fa ? "ویدیو" : "video",
            LockType.Sticker => fa ? "استیکر" : "sticker",
            LockType.Gif => fa ? "گیف" : "gif",
            LockType.File => fa ? "فایل" : "file",
            LockType.Voice => fa ? "ویس" : "voice",
            LockType.Poll => fa ? "نظرسنجی" : "poll",
            LockType.Bots => fa ? "ربات" : "bots",
            _ => fa ? "ورود" : "service",
        };
    }

    /// <summary>The locked kind this message contains, if any. Join/leave and bots are handled separately.</summary>
    public static LockType? Find(Message message, NormalizedText text, IReadOnlySet<LockType> locked)
    {
        if (locked.Count == 0) return null;

        if (locked.Contains(LockType.Forward) && message.ForwardOrigin is not null) return LockType.Forward;
        if (locked.Contains(LockType.Sticker) && message.Sticker is not null) return LockType.Sticker;
        // A GIF also carries a Document, so it is checked before files.
        if (locked.Contains(LockType.Gif) && message.Animation is not null) return LockType.Gif;
        if (locked.Contains(LockType.Photo) && message.Photo is not null) return LockType.Photo;
        if (locked.Contains(LockType.Video) && (message.Video is not null || message.VideoNote is not null)) return LockType.Video;
        if (locked.Contains(LockType.Voice) && (message.Voice is not null || message.Audio is not null)) return LockType.Voice;
        if (locked.Contains(LockType.File) && message.Document is not null && message.Animation is null) return LockType.File;
        if (locked.Contains(LockType.Poll) && message.Poll is not null) return LockType.Poll;

        if (locked.Contains(LockType.Username))
        {
            var entities = message.Entities ?? message.CaptionEntities ?? [];
            if (entities.Any(e => e.Type is MessageEntityType.Mention) || LinkDetector.ContainsUsername(text))
                return LockType.Username;
        }
        return null;
    }
}
