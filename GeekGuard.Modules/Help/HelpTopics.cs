using GeekGuard.Core.Help;
using GeekGuard.Core.Messaging;
using GeekGuard.Modules.AntiFlood;
using GeekGuard.Modules.AntiLink;
using GeekGuard.Modules.Diagnostics;
using GeekGuard.Modules.GroupSettings;
using GeekGuard.Modules.Locks;
using GeekGuard.Modules.Moderation;
using GeekGuard.Modules.Reports;
using GeekGuard.Modules.Welcome;
using GeekGuard.Modules.WordFilters;

namespace GeekGuard.Modules.Help;

/// <summary>
/// The user guide shown in the bot's private chat (📖 Guide). All pages live here so the texts are easy to review
/// and translate together; each plugin registers its own pages in its ConfigureServices.
/// </summary>
/// <remarks>Telegram HTML: only &lt;b&gt;, &lt;i&gt;, &lt;code&gt;, &lt;blockquote&gt;…; a literal &lt; must be written &amp;lt;.</remarks>
public static class HelpTopics
{
    public static readonly HelpTopic GettingStarted = new("start", GroupSettingsPlugin.Id, 10,
        new("🚀 شروع کار", "🚀 Getting started"),
        new(
            """
            <b>🚀 شروع کار</b>

            <b>۱. ربات را ادمین کن</b>
            Geek Guard را به گروه اضافه و ادمین کن، با این دو اجازه:
            • <b>Delete messages</b> (حذف پیام)
            • <b>Ban users</b> (محدود کردن اعضا)
            با /ping در گروه می‌بینی دسترسی‌ها درست است یا نه.

            <b>۲. گروه باید سوپرگروه باشد</b>
            سکوت فقط در سوپرگروه کار می‌کند. اگر گروهت معمولی است:
            تنظیمات گروه ← Chat history for new members ← <b>Visible</b>

            <b>۳. تنظیمات</b>
            همین‌جا در پیوی /start بزن و گروهت را انتخاب کن. همه‌چیز با دکمه تنظیم می‌شود.

            <b>چند نکته</b>
            • هر دستور هم با / کار می‌کند و هم با کلمه‌ی فارسی؛ مثلاً <code>/ban</code> یا «بن».
            • دستورها فقط برای ادمین‌هاست. اگر عضو عادی دستوری بنویسد، ربات هیچ واکنشی نشان نمی‌دهد.
            • هیچ پیامی از ربات در گروه نمی‌ماند: جواب‌ها، هشدارها و خوشامد خودشان پاک می‌شوند، و دستورهای ادمین هم همین‌طور.
            • 🆓 یعنی رایگان و 💎 یعنی نسخه‌ی Pro.
            """,
            """
            <b>🚀 Getting started</b>

            <b>1. Make the bot an admin</b>
            Add Geek Guard to your group as an admin with these two rights:
            • <b>Delete messages</b>
            • <b>Ban users</b> (restrict members)
            Send /ping in the group to check them.

            <b>2. Use a supergroup</b>
            Muting only works in supergroups. If yours is a basic group:
            Group settings → Chat history for new members → <b>Visible</b>

            <b>3. Settings</b>
            Send /start here in the private chat and pick your group. Everything is set with buttons.

            <b>Good to know</b>
            • Every command works with a slash and as a Persian word, e.g. <code>/ban</code> or «بن».
            • Commands are for admins only. When a member types one, the bot does not react at all.
            • Nothing the bot posts stays in the group: replies, notices and welcomes remove themselves, and so do admins' commands.
            • 🆓 means free, 💎 means Pro.
            """));

    public static readonly HelpTopic Moderation = new("mod", ModerationPlugin.Id, 20,
        new("⚠️ اخطار و مجازات", "⚠️ Warnings & penalties"),
        new(
            """
            <b>⚠️ اخطار، سکوت، اخراج و بن</b>

            <b>دستورها</b>
            • «اخطار» یا <code>/warn [دلیل]</code>
            • «حذف اخطار» یا <code>/unwarn</code>
            • «سکوت» / «میوت» یا <code>/mute</code>؛ با زمان: «سکوت ۲ ساعت»، <code>/mute 30m</code>
            • «رفع سکوت» / «آزاد» یا <code>/unmute</code>
            • «اخراج» / «کیک» یا <code>/kick</code> (می‌تواند دوباره عضو شود)
            • «بن» یا <code>/ban</code>؛ با زمان: «بن ۱ روز»
            • «رفع بن» / «آنبن» یا <code>/unban</code>

            <b>مشخص کردن کاربر</b> (یکی کافی است)
            • ریپلای روی پیامش
            • یوزرنیم: «سکوت @ali ۲ ساعت»
            • آیدی عددی: «رفع بن 123456789»
            • منشن از لیست @ (برای کسی که یوزرنیم ندارد)
            آیدی عددی بعد از هر بن و سکوت در پیام ربات نوشته می‌شود.

            <b>زمان‌ها</b>: دقیقه، ساعت، روز (یا m، h، d). کمتر از یک دقیقه پذیرفته نمی‌شود، چون تلگرام آن را دائمی حساب می‌کند.

            <b>سقف اخطار</b>
            پیش‌فرض ۳ اخطار و بعد سکوت ۲۴ ساعته. سقف، نوع مجازات و مدت آن در پنل قابل تغییر است.
            اخطارها اگر کسی ۳۰ روز اخطار جدید نگیرد صفر می‌شوند (در پنل: هیچ‌وقت، ۷، ۳۰ یا ۹۰ روز).

            <b>پاکسازی</b>
            دستور ادمین، جواب ربات و پیام متخلف بعد از ۳۰ ثانیه پاک می‌شوند. تغییر: «پاکسازی ۲۰ ثانیه» یا <code>/cleanup 20s</code>.

            این دستورها برای ادمین‌هایی است که اجازه‌ی محدود کردن اعضا دارند. روی ادمین‌ها اجرا نمی‌شوند.
            """,
            """
            <b>⚠️ Warn, mute, kick and ban</b>

            <b>Commands</b>
            • <code>/warn [reason]</code> — «اخطار»
            • <code>/unwarn</code> — «حذف اخطار»
            • <code>/mute</code>, timed: <code>/mute 30m</code>, <code>/mute 2h</code> — «سکوت»
            • <code>/unmute</code> — «رفع سکوت»
            • <code>/kick</code> (they may rejoin) — «اخراج»
            • <code>/ban</code>, timed: <code>/ban 1d</code> — «بن»
            • <code>/unban</code> — «رفع بن»

            <b>Who</b> (any one of these)
            • reply to their message
            • username: <code>/mute @ali 2h</code>
            • numeric id: <code>/unban 123456789</code>
            • a mention picked from the @ list (for people without a username)
            The numeric id is shown in the bot's message after every ban or mute.

            <b>Durations</b>: m, h, d. Less than a minute is refused, because Telegram would make it permanent.

            <b>Warning limit</b>
            By default 3 warnings lead to a 24-hour mute. Limit, penalty and length are set in the panel.
            Warnings reset when someone gets no new one for 30 days (panel: never, 7, 30 or 90 days).

            <b>Cleanup</b>
            The admin's command, the bot's reply and the offending message are removed after 30 seconds.
            Change it with <code>/cleanup 20s</code>.

            For admins allowed to restrict members. They never work on admins.
            """));

    public static readonly HelpTopic AntiLink = new("link", AntiLinkPlugin.Id, 30,
        new("🔗 ضدلینک", "🔗 Anti-link"),
        new(
            """
            <b>🔗 ضدلینک</b>

            لینک‌هایی که اعضا می‌فرستند پاک می‌شود و فرستنده اخطار می‌گیرد. این‌ها هم شناسایی می‌شوند:
            • لینک پنهان پشت متن
            • دکمه‌های لینک‌دار زیر پیام
            • ترفندهایی مثل «t . me» یا حروف شبیه به هم
            • پیامی که اول سالم فرستاده شده و بعد ویرایش شده تا لینک داشته باشد

            لینک ادمین‌ها و کانال متصل به گروه پاک نمی‌شود.
            در پنل: روشن/خاموش، و اینکه اخطار بدهد یا فقط پاک کند.
            """,
            """
            <b>🔗 Anti-link</b>

            Links from members are removed and the sender is warned. Also caught:
            • links hidden behind text
            • link buttons under a message
            • tricks like “t . me” or look-alike letters
            • a message sent clean and then edited to add a link

            Admins and the group's linked channel are never touched.
            In the panel: on/off, and whether to warn or only remove.
            """));

    public static readonly HelpTopic AntiFlood = new("flood", AntiFloodPlugin.Id, 40,
        new("🌊 ضدفلود", "🌊 Anti-flood"),
        new(
            """
            <b>🌊 ضدفلود</b>

            اگر کسی در ۱۰ ثانیه پیام‌های زیادی پشت سر هم بفرستد، <b>همه‌ی آن پیام‌ها</b> پاک می‌شود و خودش ۱۰ دقیقه ساکت می‌شود.

            <b>سطح‌ها</b> (پیام در ۱۰ ثانیه)
            • کم: ۱۰ — متوسط: ۷ (پیش‌فرض) — سخت: ۵ — خاموش

            <b>دستورها</b>
            • «ضدفلود» یا <code>/flood</code>: نمایش سطح
            • «ضدفلود سخت» یا <code>/flood strict</code> (همچنین کم / متوسط / خاموش — low / medium / off)

            ویرایش پیام شمرده نمی‌شود و ادمین‌ها معاف‌اند. مدت سکوت در پنل قابل تغییر است.
            """,
            """
            <b>🌊 Anti-flood</b>

            Whoever sends many messages within 10 seconds has <b>all of them</b> removed and is muted for 10 minutes.

            <b>Levels</b> (messages in 10 seconds)
            • low: 10 — medium: 7 (default) — strict: 5 — off

            <b>Commands</b>
            • <code>/flood</code>: show the level
            • <code>/flood strict</code> (or low / medium / off) — «ضدفلود سخت»

            Edits are not counted and admins are exempt. The mute length is set in the panel.
            """));

    public static readonly HelpTopic Locks = new("locks", LocksPlugin.Id, 50,
        new("🔒 قفل‌ها", "🔒 Locks"),
        new(
            """
            <b>🔒 قفل‌ها</b>

            هر چیزی که قفل شود، اگر عضو عادی بفرستد پاک می‌شود:
            عکس، ویدیو، استیکر، گیف، فایل، ویس، نظرسنجی، فوروارد، آیدی (@)، ربات (رباتی که عضو اضافه کند)، ورود (پیام‌های «وارد شد / خارج شد»).

            <b>دستورها</b>
            • «قفل عکس» یا <code>/lock photo</code> — چند تا با هم: «قفل عکس و استیکر»
            • «باز کردن عکس» یا <code>/unlock photo</code>
            • «قفل همه» / «باز کردن همه» یا <code>/lock all</code>
            • «قفل‌ها» یا <code>/locks</code>: وضعیت همه‌ی قفل‌ها

            از اول فقط «ربات» قفل است. در پنل هر قفل با یک کلیک باز و بسته می‌شود.
            """,
            """
            <b>🔒 Locks</b>

            Locked content sent by a member is removed:
            photo, video, sticker, gif, file, voice, poll, forward, username (@), bots (added by members), service (“joined / left” notices).

            <b>Commands</b>
            • <code>/lock photo</code> — several at once: <code>/lock photo sticker</code>
            • <code>/unlock photo</code>
            • <code>/lock all</code>, <code>/unlock all</code>
            • <code>/locks</code>: what is locked and what is allowed

            Out of the box only “bots” is locked. In the panel each lock is one tap.
            """));

    public static readonly HelpTopic WordFilter = new("words", WordFilterPlugin.Id, 55,
        new("🚫 فیلتر کلمات", "🚫 Word filter"),
        new(
            $"""
            <b>🚫 فیلتر کلمات</b>

            پیامی که یکی از این کلمه‌ها را داشته باشد پاک می‌شود و فرستنده اخطار می‌گیرد، حتی اگر کلمه را پنهان کنند:
            «ت.ب.ل.ی.غ»، «ت ب ل ی غ»، «تبلیـــغ»، «تبلییییغ» یا با ی و ک عربی.
            کلمه‌های کوتاه فقط به‌شکل کلمه‌ی کامل گرفته می‌شوند تا کلمه‌های بی‌گناه پاک نشوند (مثلاً «مسکونی»).

            <b>دستورها</b>
            • «فیلتر کلمه تبلیغ، کازینو» یا <code>/filter تبلیغ, کازینو</code>
            • «حذف فیلتر کازینو» یا <code>/unfilter کازینو</code>؛ همه: «حذف فیلتر همه»
            • «فیلترها» یا <code>/filters</code>: لیست (زیر اسپویلر)

            بدون ویرگول کل متن یک عبارت حساب می‌شود: «فیلتر کلمه خیلی بد» فقط «خیلی بد» را فیلتر می‌کند.

            🆓 تا {WordFilterSettings.FreeLimit} کلمه رایگان. 💎 بیشتر از آن در نسخه‌ی Pro (به‌زودی).
            """,
            $"""
            <b>🚫 Word filter</b>

            Messages containing a banned word are removed and the sender is warned, even when the word is disguised:
            dotted, spaced out, stretched, repeated letters or Arabic letter variants.
            Short words only match as whole words, so innocent longer words are left alone.

            <b>Commands</b>
            • <code>/filter word1, word2</code> — «فیلتر کلمه …»
            • <code>/unfilter word</code>; everything: <code>/unfilter all</code>
            • <code>/filters</code>: the list (behind a spoiler)

            Without commas the whole text is one phrase: <code>/filter very bad</code> bans “very bad” only.

            🆓 Up to {WordFilterSettings.FreeLimit} words free. 💎 More with Pro (coming soon).
            """));

    public static readonly HelpTopic Welcome = new("welcome", WelcomePlugin.Id, 70,
        new("👋 خوشامد و قوانین", "👋 Welcome & rules"),
        new(
            """
            <b>👋 خوشامدگویی</b>
            به هر کسی که وارد شود خوشامد می‌گوید. فقط آخرین خوشامد در گروه می‌ماند و خودش هم بعد از ۵ دقیقه پاک می‌شود (در پنل قابل تغییر).
            • «خوشامد» یا <code>/welcome</code>: وضعیت و پیش‌نمایش
            • «تنظیم خوشامد» و بعد متن (یا ریپلای روی پیام متن)؛ می‌توانی از <code>{name}</code> و <code>{group}</code> و <code>{count}</code> استفاده کنی
            • «خوشامد روشن» / «خوشامد خاموش» — «خوشامد پیش‌فرض»

            <b>📜 قوانین</b>
            • «تنظیم قوانین» و از خط بعد متن قوانین (یا ریپلای روی پیام قوانین)
            • «قوانین» یا <code>/rules</code>: هر کسی می‌تواند بزند؛ قوانین در پیوی ربات باز می‌شود تا گروه شلوغ نشود
            • «حذف قوانین»
            اگر قوانین ثبت شده باشد، زیر خوشامد دکمه‌ی «📜 قوانین گروه» می‌آید.

            هر دو را از پنل هم می‌توانی بنویسی.
            """,
            """
            <b>👋 Welcome</b>
            Greets everyone who joins. Only the latest welcome stays, and it removes itself after 5 minutes (change it in the panel).
            • <code>/welcome</code>: status and preview
            • <code>/setwelcome text</code> (or reply to a message holding it); use <code>{name}</code>, <code>{group}</code>, <code>{count}</code>
            • <code>/welcome on</code>, <code>/welcome off</code>, <code>/resetwelcome</code>

            <b>📜 Rules</b>
            • <code>/setrules text</code> (or reply to a message holding them)
            • <code>/rules</code>: anyone may ask; the rules open in a private chat with the bot, so the group stays quiet
            • <code>/clearrules</code>
            With rules set, the welcome gets a “📜 Rules” button.

            Both can also be written from the panel.
            """));

    public static readonly HelpTopic Reports = new("reports", ReportsPlugin.Id, 60,
        new("🚩 گزارش به ادمین", "🚩 Reports"),
        new(
            """
            <b>🚩 گزارش به ادمین</b>

            هر عضوی می‌تواند روی پیامی ریپلای کند و بنویسد «گزارش» یا <code>/report [توضیح]</code>.
            • دستور گزارش فوراً پاک می‌شود تا کسی نفهمد چه کسی گزارش داده.
            • گزارش در <b>پیوی ربات</b> به ادمین‌ها می‌رسد، با متن پیام، لینک آن و دکمه‌های 🗑 حذف، 🔇 سکوت ۲۴ ساعت، ⛔ بن و ✔️ بی‌مورد.
            • هر ادمینی که اول دکمه را بزند کار را انجام می‌دهد و پیام گزارش برای بقیه‌ی ادمین‌ها هم به‌روز می‌شود.
            • هر نفر هر دقیقه یک گزارش؛ پیامی که چند نفر گزارش کنند فقط یک بار به ادمین‌ها می‌رسد.

            ⚠️ فقط ادمین‌هایی گزارش می‌گیرند که یک بار ربات را در پیوی /start کرده باشند.
            """,
            """
            <b>🚩 Reports</b>

            Any member can reply to a message with <code>/report [note]</code> or «گزارش».
            • The report command is removed at once, so nobody sees who reported.
            • Admins get it in a <b>private chat with the bot</b>, with the message, a link to it, and 🗑 delete, 🔇 mute 24h, ⛔ ban and ✔️ dismiss buttons.
            • The first admin to press acts; every admin's copy then shows what was done and by whom.
            • One report per member per minute; a message reported by several members reaches the admins once.

            ⚠️ Only admins who have sent /start to the bot once receive reports.
            """));

    public static readonly HelpTopic Language = new("lang", GroupSettingsPlugin.Id, 80,
        new("🌐 زبان", "🌐 Language"),
        new(
            """
            <b>🌐 زبان</b>

            <b>زبان پیام‌های ربات در گروه</b>
            • «زبان فارسی» / «زبان انگلیسی» یا <code>/lang fa</code>، <code>/lang en</code>
            گروهی که اسم فارسی دارد خودکار فارسی می‌شود.

            <b>زبان همین پنل و راهنما</b>
            از صفحه‌ی اول پنل، دکمه‌ی 🌐. زبان پنل برای هر ادمین جداست.
            """,
            """
            <b>🌐 Language</b>

            <b>Language of the bot's messages in a group</b>
            • <code>/lang en</code>, <code>/lang fa</code> — «زبان انگلیسی»
            Groups with a Persian name start in Persian.

            <b>Language of this panel and guide</b>
            The 🌐 button on the panel's first screen. Each admin has their own.
            """));

    public static readonly HelpTopic Diagnostics = new("ping", DiagnosticsPlugin.Id, 90,
        new("🩺 عیب‌یابی", "🩺 Troubleshooting"),
        new(
            """
            <b>🩺 عیب‌یابی</b>

            <code>/ping</code> در گروه (فقط ادمین): نشان می‌دهد ربات روشن است، دسترسی‌هایش درست است یا نه، و کدام بخش‌ها در گروه فعال‌اند.

            <b>مشکل‌های رایج</b>
            • <b>«انجام نشد»</b> هنگام سکوت یا بن: ربات اجازه‌ی Ban users ندارد، یا گروه سوپرگروه نیست.
            • <b>گروه در پنل نیست</b>: یک پیام در گروه بده و دوباره /start بزن.
            • <b>گزارش‌ها نمی‌رسد</b>: یک بار به ربات /start بده.
            • <b>دستور جواب نمی‌دهد</b>: فقط ادمین‌ها جواب می‌گیرند؛ برای سکوت و بن اجازه‌ی محدود کردن اعضا لازم است.
            """,
            """
            <b>🩺 Troubleshooting</b>

            <code>/ping</code> in the group (admins): shows the bot is alive, whether its rights are right, and which features run there.

            <b>Common problems</b>
            • <b>“That failed”</b> on mute or ban: the bot lacks Ban users, or the group is not a supergroup.
            • <b>Group missing from the panel</b>: post a message in the group, then /start again.
            • <b>No reports arrive</b>: send /start to the bot once.
            • <b>No answer to a command</b>: only admins get answers; mute and ban need the restrict-members right.
            """));
}
