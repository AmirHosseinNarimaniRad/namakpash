using System.Globalization;

namespace Splitt.App.Helpers;

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
