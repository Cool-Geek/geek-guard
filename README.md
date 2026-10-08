# Geek Guard

**Telegram group protection with first-class Persian spam detection.**
by [Cool Geek](https://github.com/Cool-Geek)

> 🚧 Under active development.

[فارسی](#فارسی) · [English](#english)

---

## English

Geek Guard (@geek_guard_bot) keeps Telegram groups clean: anti-flood, anti-link (including disguised links),
Persian-aware word filters, content locks, warnings and a full settings panel inside Telegram.

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
5. Run the `GeekGuard.Bot` project.

### Configuration

| Key | Meaning | Where |
| --- | --- | --- |
| `Telegram:BotToken` | Token from @BotFather | User Secrets / env `Telegram__BotToken` |
| `Telegram:Proxy` | Optional proxy URL | User Secrets / env `Telegram__Proxy` |
| `ConnectionStrings:GeekGuard` | PostgreSQL connection string | User Secrets / env `ConnectionStrings__GeekGuard` |

### Database migrations

Schema changes are plain SQL files named `NNNN_description.sql` in a module's `Migrations` folder,
embedded in its assembly and applied in order at startup. Applied versions are recorded in `schema_migrations`.
Never edit a migration that has been released; add a new one.

---

## فارسی

Geek Guard (@geek_guard_bot) گروه‌های تلگرام را تمیز نگه می‌دارد: ضدفلود، ضدلینک (حتی لینک‌های مخفی‌شده)،
فیلتر کلمات با درک درست متن فارسی، قفل‌ها، اخطار، و پنل کامل تنظیمات داخل خود تلگرام.

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
4. پروژه‌ی `GeekGuard.Bot` را اجرا کنید.
