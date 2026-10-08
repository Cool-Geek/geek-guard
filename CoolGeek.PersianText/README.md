# CoolGeek.PersianText

Persian-aware text normalization, link detection and word filtering for .NET, built to see through the tricks
spammers use. Part of [Geek Guard](https://github.com/Cool-Geek/geek-guard) by Cool Geek.

```csharp
using CoolGeek.PersianText;

var text = NormalizedText.From("تبليــــغ ویژه: 𝐭 . 𝐦𝐞 / channel");

text.Value;                          // "تبلیغ ویژه: t . me / channel"
LinkDetector.ContainsLink(text);     // true  (disguised t.me link)

var filter = new WordFilter(["تبلیغ", "casino"]);
filter.FindMatch(text);              // "تبلیغ"
filter.FindMatch("ت.ب.ل.ی.غ");        // "تبلیغ"
filter.FindMatch("casssino");        // "casino"
```

What normalization handles:

| Trick | Example | Becomes |
| --- | --- | --- |
| Arabic letter variants | تبليغ، كانال | تبلیغ، کانال |
| Tatweel (kashida) | تبلیــــغ | تبلیغ |
| Zero-width and direction characters | تبلی‌غ (hidden ZWNJ) | تبلیغ |
| Persian and Arabic digits | ۰۹۱۲ ٣ | 0912 3 |
| Fancy Unicode letters | 𝐭.𝐦𝐞 | t.me |
| Cyrillic / Greek look-alikes | Тelegrаm | telegram |
| Diacritics | تَبلیغ | تبلیغ |

---

# CoolGeek.PersianText (فارسی)

کتابخانه‌ی .NET برای یکدست‌سازی متن فارسی، تشخیص لینک و فیلتر کلمات؛ ساخته شده تا ترفندهای اسپمرها را ببیند:
ی و ک عربی، کشیده، نیم‌فاصله و کاراکترهای نامرئی، اعداد فارسی، حروف فانتزی یونیکد و لینک‌های مخفی مثل «t . me» یا «تی دات می».
