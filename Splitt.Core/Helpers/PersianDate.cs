using System.Globalization;

namespace Splitt.Core.Helpers;

/// <summary>Jalali (Shamsi) calendar display with English digits. Storage stays Gregorian.</summary>
public static class PersianDate
{
    private static readonly PersianCalendar Calendar = new();

    public static readonly string[] MonthNames =
    [
        "فروردین", "اردیبهشت", "خرداد", "تیر", "مرداد", "شهریور",
        "مهر", "آبان", "آذر", "دی", "بهمن", "اسفند",
    ];

    /// <summary>Local date → "1404/05/15" (English digits).</summary>
    public static string ToDisplay(DateTime date)
    {
        var (y, m, d) = ToJalali(date);
        return $"{y:0000}/{m:00}/{d:00}";
    }

    /// <summary>
    /// Local date → "1404/05/15", or "1404/05/15 14:32" when the value carries a
    /// time of day. Expenses saved before times were recorded sit at local midnight;
    /// they keep showing the date alone instead of a meaningless "00:00".
    /// Wrapped in an LTR isolate so the date and time do not swap inside an RTL line.
    /// </summary>
    public static string ToDisplayWithTime(DateTime date)
    {
        var text = ToDisplay(date);
        if (date.TimeOfDay != TimeSpan.Zero)
            text += " " + date.ToString("HH:mm", CultureInfo.InvariantCulture);
        return Bidi.Ltr(text);
    }

    /// <summary>Local date → "15 مرداد 1404".</summary>
    public static string ToLongDisplay(DateTime date)
    {
        var (y, m, d) = ToJalali(date);
        return $"{d} {MonthNames[m - 1]} {y}";
    }

    public static (int Year, int Month, int Day) ToJalali(DateTime date) =>
        (Calendar.GetYear(date), Calendar.GetMonth(date), Calendar.GetDayOfMonth(date));

    public static DateTime FromJalali(int year, int month, int day) =>
        Calendar.ToDateTime(year, month, day, 0, 0, 0, 0);

    public static int DaysInMonth(int year, int month) =>
        Calendar.GetDaysInMonth(year, month);
}
