namespace GeekGuard.Core.Messaging;

/// <summary>Times shown to people and schedules run in Iran time for now (Asia/Tehran).</summary>
public static class LocalTime
{
    public static readonly TimeZoneInfo Zone = Find();

    public static DateTimeOffset Now(TimeProvider clock) => TimeZoneInfo.ConvertTime(clock.GetUtcNow(), Zone);

    public static DateTimeOffset ToLocal(DateTimeOffset utc) => TimeZoneInfo.ConvertTime(utc, Zone);

    /// <summary>"23:30".</summary>
    public static string Clock(DateTimeOffset utc) => ToLocal(utc).ToString("HH:mm", System.Globalization.CultureInfo.InvariantCulture);

    /// <summary>
    /// When something happens, as people say it: just the time if it is within a day, otherwise the date too
    /// (Persian calendar in Persian: «1405/07/20 08:00»).
    /// </summary>
    public static string When(DateTimeOffset utc, string lang, TimeProvider clock)
    {
        var local = ToLocal(utc);
        if (utc - clock.GetUtcNow() < TimeSpan.FromHours(20)) return Clock(utc);
        if (lang == Languages.English) return local.ToString("yyyy-MM-dd HH:mm", System.Globalization.CultureInfo.InvariantCulture);

        var persian = new System.Globalization.PersianCalendar();
        var d = local.DateTime;
        return $"{persian.GetYear(d)}/{persian.GetMonth(d):00}/{persian.GetDayOfMonth(d):00} {Clock(utc)}";
    }

    private static TimeZoneInfo Find()
    {
        // IANA id on Linux and on Windows with ICU; the Windows id as a fallback.
        foreach (var id in new[] { "Asia/Tehran", "Iran Standard Time" })
        {
            try
            {
                return TimeZoneInfo.FindSystemTimeZoneById(id);
            }
            catch (Exception ex) when (ex is TimeZoneNotFoundException or InvalidTimeZoneException)
            {
            }
        }
        return TimeZoneInfo.CreateCustomTimeZone("Tehran", TimeSpan.FromHours(3.5), "Tehran", "Tehran");
    }
}
