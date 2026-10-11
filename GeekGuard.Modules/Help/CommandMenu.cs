using GeekGuard.Core.Commands;

namespace GeekGuard.Modules.Help;

/// <summary>
/// The "/" menu group admins see (members see none). One list per plugin, registered in that plugin, kept here so
/// the descriptions are easy to review together. Each description ends with the Persian keyword that does the same.
/// </summary>
public static class CommandMenu
{
    private static BotCommandEntry Admin(string command, string fa, string en, int order) =>
        new(command, new(fa, en), CommandAudience.GroupAdmins, order);

    public static readonly BotCommandEntry[] Moderation =
    [
        Admin("warn", "⚠️ اخطار (ریپلای، @آیدی یا آیدی عددی) — «اخطار»", "⚠️ Warn (reply, @user or id)", 10),
        Admin("unwarn", "✅ پاک کردن اخطارها — «حذف اخطار»", "✅ Clear warnings", 11),
        Admin("mute", "🔇 سکوت، مثلاً /mute 2h — «سکوت ۲ ساعت»", "🔇 Mute, e.g. /mute 2h", 12),
        Admin("unmute", "🔊 رفع سکوت — «رفع سکوت»", "🔊 Unmute", 13),
        Admin("kick", "👢 اخراج (می‌تواند برگردد) — «اخراج»", "👢 Kick (may rejoin)", 14),
        Admin("ban", "⛔ بن، مثلاً /ban 1d — «بن»", "⛔ Ban, e.g. /ban 1d", 15),
        Admin("unban", "✅ رفع بن (با آیدی عددی هم) — «رفع بن»", "✅ Unban (also by numeric id)", 16),
        Admin("cleanup", "🧹 زمان پاک شدن دستور و جواب — «پاکسازی ۳۰ ثانیه»", "🧹 When commands and replies are removed", 60),
    ];

    public static readonly BotCommandEntry[] GroupClose =
    [
        Admin("close", "🔒 بستن گروه، مثلاً /close 2h — «بستن گروه»", "🔒 Close the group, e.g. /close 2h", 20),
        Admin("open", "🔓 باز کردن گروه — «باز کردن گروه»", "🔓 Open the group", 21),
    ];

    public static readonly BotCommandEntry[] Locks =
    [
        Admin("lock", "🔒 قفل، مثلاً /lock photo یا /lock all — «قفل عکس»", "🔒 Lock, e.g. /lock photo or /lock all", 30),
        Admin("unlock", "🔓 باز کردن قفل — «باز کردن عکس»", "🔓 Unlock", 31),
        Admin("locks", "📋 وضعیت قفل‌ها — «قفل‌ها»", "📋 What is locked", 32),
    ];

    public static readonly BotCommandEntry[] WordFilter =
    [
        Admin("filter", "🚫 فیلتر کلمه، چندتا با ویرگول — «فیلتر کلمه …»", "🚫 Ban words, several with commas", 40),
        Admin("unfilter", "♻️ حذف از فیلتر — «حذف فیلتر …»", "♻️ Unban a word", 41),
        Admin("filters", "📋 لیست کلمه‌های فیلتر — «فیلترها»", "📋 Banned words", 42),
    ];

    public static readonly BotCommandEntry[] AntiFlood =
    [
        Admin("flood", "🌊 ضدفلود: off / low / medium / strict — «ضدفلود سخت»", "🌊 Anti-flood: off / low / medium / strict", 50),
    ];

    public static readonly BotCommandEntry[] Welcome =
    [
        Admin("welcome", "👋 خوشامد: وضعیت، on یا off — «خوشامد»", "👋 Welcome: status, on or off", 70),
        Admin("setwelcome", "✏️ متن خوشامد ({name} {group} {count}) — «تنظیم خوشامد …»", "✏️ Welcome text ({name} {group} {count})", 71),
        Admin("resetwelcome", "↩️ خوشامد پیش‌فرض — «خوشامد پیش‌فرض»", "↩️ Default welcome", 72),
    ];

    public static readonly BotCommandEntry[] Rules =
    [
        Admin("setrules", "📜 ثبت قوانین — «تنظیم قوانین …»", "📜 Set the rules", 80),
        Admin("rules", "📜 دکمه‌ی قوانین — «قوانین»", "📜 Show the rules button", 81),
        Admin("clearrules", "🗑 حذف قوانین — «حذف قوانین»", "🗑 Remove the rules", 82),
    ];

    public static readonly BotCommandEntry[] GroupSettings =
    [
        Admin("lang", "🌐 زبان ربات در گروه: fa یا en — «زبان فارسی»", "🌐 Bot language here: fa or en", 90),
        Admin("help", "📖 راهنمای کامل در پیوی — «راهنما»", "📖 Full guide in private chat", 99),
    ];

    public static readonly BotCommandEntry[] Diagnostics =
    [
        Admin("ping", "🩺 وضعیت ربات و دسترسی‌هایش", "🩺 Bot status and rights", 95),
    ];
}
