using System.Text.RegularExpressions;
using CoolGeek.PersianText;
using GeekGuard.Core.Messaging;

namespace GeekGuard.Modules.Moderation;

/// <summary>Reads and writes durations the way admins type them: "30m", "2h", "1d", "۲ ساعت", "30 دقیقه", "1 روز".</summary>
public static partial class Duration
{
    public static readonly TimeSpan Max = TimeSpan.FromDays(366);

    // Text is normalized first, so Persian digits are already ASCII.
    [GeneratedRegex(@"^(?<n>\d{1,4})\s*(?<unit>m|min|mins|minute|minutes|دقیقه|h|hr|hrs|hour|hours|ساعت|d|day|days|روز)$",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex Pattern();

    /// <summary>Parses a duration, or returns null if the text is not one. Values over a year are capped.</summary>
    public static TimeSpan? Parse(string? text)
    {
        if (string.IsNullOrWhiteSpace(text)) return null;

        var match = Pattern().Match(PersianNormalizer.Normalize(text));
        if (!match.Success) return null;

        var n = int.Parse(match.Groups["n"].Value);
        if (n == 0) return null;

        var span = match.Groups["unit"].Value switch
        {
            "m" or "min" or "mins" or "minute" or "minutes" or "دقیقه" => TimeSpan.FromMinutes(n),
            "h" or "hr" or "hrs" or "hour" or "hours" or "ساعت" => TimeSpan.FromHours(n),
            _ => TimeSpan.FromDays(n),
        };
        return span > Max ? Max : span;
    }

    private static readonly Localized Minutes = new("{0} دقیقه", "{0} min");
    private static readonly Localized Hours = new("{0} ساعت", "{0} h");
    private static readonly Localized Days = new("{0} روز", "{0} day(s)");

    /// <summary>"2 ساعت" / "2 h", picking the largest whole unit.</summary>
    public static string Format(TimeSpan span, string lang)
    {
        if (span.TotalDays >= 1 && span.TotalDays % 1 == 0) return Days.Format(lang, (int)span.TotalDays);
        if (span.TotalHours >= 1 && span.TotalHours % 1 == 0) return Hours.Format(lang, (int)span.TotalHours);
        return Minutes.Format(lang, (int)Math.Ceiling(span.TotalMinutes));
    }
}
