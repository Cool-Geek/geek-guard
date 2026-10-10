# Geek Guard

**Telegram group protection with first-class Persian spam detection.**
by [Cool Geek](https://github.com/Cool-Geek) · [@geek_guard_bot](https://t.me/geek_guard_bot)

> 🚧 Under active development.

[فارسی](#فارسی) · [English](#english)

---

## English

Geek Guard keeps Telegram groups clean: warnings and penalties, anti-link (including disguised links),
anti-flood, content locks, a Persian-aware word filter, welcome messages, rules, member reports,
and a full settings panel in the bot's private chat. Everything works in Persian and English.

### Features

🆓 free · 💎 Pro

| | Feature | What it does |
| --- | --- | --- |
| 🆓 | Warnings & penalties | Warn, mute, kick, ban (timed or permanent). Target a member by reply, @username, numeric id or mention. Warning limit with an automatic penalty; warnings expire after a quiet period. |
| 🆓 | Anti-link | Removes links, including links hidden behind text, link buttons, “t . me” tricks and messages edited to add a link. |
| 🆓 | Anti-flood | Removes the whole burst and mutes the sender. Four levels. |
| 🆓 | Locks | Photos, videos, stickers, GIFs, files, voice, polls, forwards, @usernames, bots added by members, join/leave notices. “Lock all” in one go. |
| 🆓 | Word filter | Catches disguised words (dotted, spaced, stretched, Arabic letters) without hitting innocent words. Up to 20 words. |
| 💎 | Word filter, more words | More than 20 words *(coming soon)*. |
| 🆓 | Welcome & rules | Custom welcome with `{name}`, `{group}`, `{count}`; only the latest welcome stays. Rules open in the private chat. |
| 🆓 | Reports | Members report a message; admins get it privately with delete / mute / ban / dismiss buttons. |
| 🆓 | Settings panel | `/start` in the private chat: every setting with buttons, view-only for admins without the restrict right. |
| 🆓 | Guide | `/help` in the private chat: every command explained, in both languages. |

Admin commands work with a slash and as Persian words (`/ban` or «بن»). Members typing commands get no reaction,
and the admin's command and the bot's reply delete themselves after a few seconds.

### Commands at a glance

| Command | Persian keyword | |
| --- | --- | --- |
| `/warn [reason]`, `/unwarn` | «اخطار»، «حذف اخطار» | reply, `@user` or numeric id |
| `/mute [30m\|2h\|1d]`, `/unmute` | «سکوت ۲ ساعت»، «رفع سکوت» | |
| `/kick` | «اخراج» | they may rejoin |
| `/ban [1d]`, `/unban` | «بن»، «رفع بن» | |
| `/cleanup 20s` | «پاکسازی ۲۰ ثانیه» | when commands and replies are removed |
| `/flood [off\|low\|medium\|strict]` | «ضدفلود سخت» | |
| `/lock photo`, `/unlock photo`, `/lock all`, `/locks` | «قفل عکس»، «باز کردن عکس»، «قفل همه»، «قفل‌ها» | |
| `/filter a, b`, `/unfilter a`, `/filters` | «فیلتر کلمه …»، «حذف فیلتر …»، «فیلترها» | |
| `/welcome [on\|off]`, `/setwelcome text`, `/resetwelcome` | «خوشامد»، «تنظیم خوشامد …» | |
| `/setrules text`, `/rules`, `/clearrules` | «تنظیم قوانین …»، «قوانین» | `/rules` works for everyone |
| `/report [note]` | «گزارش» | for members, as a reply |
| `/lang fa\|en` | «زبان فارسی»، «زبان انگلیسی» | |
| `/ping` | | rights and active features |
| `/help` | «راهنما» | opens the guide |

### Run it locally

Requirements: .NET 8 SDK, PostgreSQL.

1. Create a **separate test bot** in [@BotFather](https://t.me/BotFather) and turn **Group Privacy off**.
2. Store its token in User Secrets (it never touches the repository):
   ```
   cd GeekGuard.Bot
   dotnet user-secrets set "Telegram:BotToken" "123456:ABC..."
   ```
   In Visual Studio: right-click the project → **Manage User Secrets**.
3. If your network cannot reach Telegram, set a proxy the same way:
   ```
   dotnet user-secrets set "Telegram:Proxy" "socks5://127.0.0.1:10808"
   ```
4. Point it at PostgreSQL. The database is created and migrated automatically on first run:
   ```
   dotnet user-secrets set "ConnectionStrings:GeekGuard" "Host=localhost;Port=5432;Database=geekguard;Username=postgres;Password=..."
   ```
5. Run the `GeekGuard.Bot` project, make the bot an admin of a group (delete messages + ban users),
   and send it `/start` in a private chat.

### Configuration

| Key | Meaning | Where |
| --- | --- | --- |
| `Telegram:BotToken` | Token from @BotFather | User Secrets / env `Telegram__BotToken` |
| `Telegram:Proxy` | Optional proxy URL | User Secrets / env `Telegram__Proxy` |
| `ConnectionStrings:GeekGuard` | PostgreSQL connection string | User Secrets / env `ConnectionStrings__GeekGuard` |

Never put the token in `appsettings.json`: this repository is public.

### Project layout

| Project | |
| --- | --- |
| `GeekGuard.Bot` | The host: Telegram connection, update routing, the settings panel and the guide. |
| `GeekGuard.Core` | Shared services: database, groups, admins, plugin state, Telegram actions, extension points. |
| `GeekGuard.Modules` | The free plugins, one folder each. |
| `CoolGeek.PersianText` | Persian text normalization, link detection and word filtering (stand-alone library). |

### Writing a plugin

A plugin implements `IGeekGuardPlugin` and registers what it needs in `ConfigureServices`:

- `AddGroupMessageHandler<T>()`: sees group messages, in pipeline order (commands → membership → flood → content filters).
- `AddPanelSection<T>()`: a section of the settings panel (implement `IPanelTextInput` to also take typed text).
- `AddHelpTopic(...)`: a page of the guide; 🆓/💎 comes from the plugin's tier.
- `AddCallbackHandler<T>()`, `AddStartLinkHandler<T>()`: inline buttons and `t.me/bot?start=…` links.
- `AddPluginMigrations(id, GetType())`: SQL files in the plugin's `Migrations` folder.

Pro plugins are built the same way in a separate private repository and dropped into `plugins/<Name>/<Name>.dll`.

### Database migrations

Schema changes are plain SQL files named `NNNN_description.sql` in a module's `Migrations` folder,
embedded in its assembly and applied in order at startup. Applied versions are recorded in `schema_migrations`.
Never edit a migration that has been released; add a new one.

### Privacy

No message text is ever stored. The database keeps: people who started the bot (id, name, @username, language);
for each group, the ids, names and @usernames of people seen there (so admins can name them), warning counts and
settings; and for reports, the ids of the message, its author and the reporter.

### License

[AGPL-3.0](LICENSE).

---

## فارسی

Geek Guard گروه‌های تلگرام را تمیز نگه می‌دارد: اخطار و مجازات، ضدلینک (حتی لینک‌های پنهان)، ضدفلود، قفل‌ها،
فیلتر کلمات با درک درست متن فارسی، خوشامدگویی، قوانین، گزارش اعضا و پنل کامل تنظیمات در پیوی ربات.
همه‌چیز فارسی و انگلیسی است.

### امکانات

🆓 رایگان · 💎 نسخه‌ی Pro

| | امکان | توضیح |
| --- | --- | --- |
| 🆓 | اخطار و مجازات | اخطار، سکوت، اخراج و بن (موقت یا دائمی). مشخص کردن کاربر با ریپلای، @یوزرنیم، آیدی عددی یا منشن. سقف اخطار با مجازات خودکار؛ اخطارها بعد از مدتی بی‌تخلفی صفر می‌شوند. |
| 🆓 | ضدلینک | لینک‌ها را پاک می‌کند، حتی لینک پشت متن، دکمه‌ی لینک‌دار، ترفند «t . me» و پیامی که بعداً ویرایش شده. |
| 🆓 | ضدفلود | همه‌ی پیام‌های پشت‌سرهم را پاک و فرستنده را ساکت می‌کند. چهار سطح. |
| 🆓 | قفل‌ها | عکس، ویدیو، استیکر، گیف، فایل، ویس، نظرسنجی، فوروارد، آیدی، ربات، پیام ورود و خروج. «قفل همه» با یک دستور. |
| 🆓 | فیلتر کلمات | کلمه‌های پنهان‌شده (نقطه‌دار، فاصله‌دار، کشیده، با حروف عربی) را می‌گیرد بدون اینکه کلمه‌های بی‌گناه پاک شوند. تا ۲۰ کلمه. |
| 💎 | فیلتر کلمات، کلمه‌ی بیشتر | بیش از ۲۰ کلمه *(به‌زودی)*. |
| 🆓 | خوشامد و قوانین | خوشامد دلخواه با `{name}`، `{group}` و `{count}`؛ فقط آخرین خوشامد می‌ماند. قوانین در پیوی ربات باز می‌شود. |
| 🆓 | گزارش به ادمین | اعضا پیام را گزارش می‌دهند؛ ادمین‌ها در پیوی با دکمه‌های حذف / سکوت / بن / بی‌مورد می‌گیرند. |
| 🆓 | پنل تنظیمات | `/start` در پیوی: همه‌ی تنظیمات با دکمه؛ ادمین بدون اجازه‌ی محدودسازی فقط می‌بیند. |
| 🆓 | راهنما | `/help` در پیوی: توضیح همه‌ی دستورها، دو زبانه. |

دستورهای ادمین هم با / کار می‌کنند و هم با کلمه‌ی فارسی (`/ban` یا «بن»). اگر عضو عادی دستور بنویسد ربات واکنشی
نشان نمی‌دهد، و دستور ادمین و جواب ربات بعد از چند ثانیه خودشان پاک می‌شوند. جدول دستورها در بخش انگلیسی بالاست
و توضیح کامل هر کدام در راهنمای داخل ربات (`/help`).

### اجرا روی سیستم خودتان

پیش‌نیاز: .NET 8 SDK و PostgreSQL.

1. در [@BotFather](https://t.me/BotFather) یک **ربات تست جدا** بسازید و **Group Privacy** را خاموش کنید.
2. توکن را در User Secrets بگذارید تا هیچ‌وقت وارد گیت نشود:
   در Visual Studio روی پروژه راست‌کلیک ← **Manage User Secrets**، و این را بنویسید:
   ```json
   { "Telegram": { "BotToken": "123456:ABC..." } }
   ```
3. اگر شبکه‌تان به تلگرام دسترسی ندارد، پروکسی و رشته‌ی اتصال دیتابیس را هم همان‌جا اضافه کنید.
   دیتابیس در اولین اجرا خودکار ساخته می‌شود:
   ```json
   {
     "Telegram": { "BotToken": "123456:ABC...", "Proxy": "socks5://127.0.0.1:10808" },
     "ConnectionStrings": {
       "GeekGuard": "Host=localhost;Port=5432;Database=geekguard;Username=postgres;Password=..."
     }
   }
   ```
4. پروژه‌ی `GeekGuard.Bot` را اجرا کنید، ربات را با اجازه‌ی حذف پیام و محدود کردن اعضا ادمین گروه کنید
   و در پیوی ربات `/start` بزنید.

هرگز توکن را در `appsettings.json` نگذارید: این مخزن عمومی است.

### حریم خصوصی

متن هیچ پیامی ذخیره نمی‌شود. دیتابیس فقط این‌ها را نگه می‌دارد: کسانی که ربات را استارت کرده‌اند (آیدی، اسم،
یوزرنیم، زبان)؛ برای هر گروه آیدی، اسم و یوزرنیم افرادی که آنجا دیده شده‌اند (تا ادمین بتواند با یوزرنیم صدایشان کند)،
تعداد اخطارها و تنظیمات؛ و برای گزارش‌ها شماره‌ی پیام و آیدی نویسنده و گزارش‌دهنده.

### مجوز

[AGPL-3.0](LICENSE).
