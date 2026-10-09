using GeekGuard.Core.Messaging;

namespace GeekGuard.Modules.Moderation;

/// <summary>Everything the moderation module says, in both languages.</summary>
internal static class ModerationTexts
{
    public static readonly Localized OnlyAdmins = new(
        "⛔ این دستور فقط برای ادمین‌هایی است که اجازه‌ی محدود کردن اعضا را دارند.",
        "⛔ Only admins who can restrict members may use this.");

    public static readonly Localized ReplyNeeded = new(
        "↩️ روی پیام همان کاربر ریپلای کن و دستور را بفرست.",
        "↩️ Reply to that member's message with the command.");

    public static readonly Localized CannotTargetAdmin = new(
        "⛔ این کار روی ادمین‌ها ممکن نیست.",
        "⛔ That can't be done to an admin.");

    public static readonly Localized InvalidDuration = new(
        "⏱ مدت را این‌طور بنویس: 30m، 2h، 1d یا «۲ ساعت».",
        "⏱ Write the duration like 30m, 2h or 1d.");

    public static readonly Localized Failed = new(
        "⚠️ انجام نشد. مطمئن شو که من ادمین هستم و اجازه‌ی «محدود کردن اعضا» دارم.",
        "⚠️ That failed. Make sure I'm an admin with the “restrict members” right.");

    public static readonly Localized Warned = new("⚠️ {0} اخطار گرفت ({1} از {2}).", "⚠️ {0} was warned ({1}/{2}).");
    public static readonly Localized WarnReason = new("دلیل: {0}", "Reason: {0}");
    public static readonly Localized WarnLimitMuted = new("🔇 {0} به {1} اخطار رسید و برای {2} ساکت شد.", "🔇 {0} reached {1} warnings and was muted for {2}.");
    public static readonly Localized WarnLimitKicked = new("👢 {0} به {1} اخطار رسید و از گروه اخراج شد.", "👢 {0} reached {1} warnings and was removed from the group.");
    public static readonly Localized WarnLimitBanned = new("⛔ {0} به {1} اخطار رسید و بن شد.", "⛔ {0} reached {1} warnings and was banned.");
    public static readonly Localized Unwarned = new("✅ اخطارهای {0} پاک شد.", "✅ Cleared {0}'s warnings.");
    public static readonly Localized NoWarnings = new("ℹ️ {0} اخطاری نداشت.", "ℹ️ {0} had no warnings.");

    public static readonly Localized Muted = new("🔇 {0} ساکت شد.", "🔇 {0} was muted.");
    public static readonly Localized MutedFor = new("🔇 {0} برای {1} ساکت شد.", "🔇 {0} was muted for {1}.");
    public static readonly Localized Unmuted = new("🔊 {0} دوباره می‌تواند پیام بدهد.", "🔊 {0} can talk again.");
    public static readonly Localized Kicked = new("👢 {0} از گروه اخراج شد (می‌تواند دوباره عضو شود).", "👢 {0} was removed from the group (they may rejoin).");
    public static readonly Localized Banned = new("⛔ {0} بن شد.", "⛔ {0} was banned.");
    public static readonly Localized BannedFor = new("⛔ {0} برای {1} بن شد.", "⛔ {0} was banned for {1}.");
    public static readonly Localized Unbanned = new("✅ {0} از بن خارج شد و می‌تواند دوباره عضو شود.", "✅ {0} was unbanned and may rejoin.");
}
