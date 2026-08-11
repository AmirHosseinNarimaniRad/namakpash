using System.Globalization;
using Splitt.Core.Helpers;

namespace Splitt.Tests;

public class PersianDateTests
{
    // 2025-08-06 → 1404/05/15
    private static readonly DateTime Day = new(2025, 8, 6, 0, 0, 0, DateTimeKind.Local);

    [Fact]
    public void ToDisplay_UsesJalaliWithEnglishDigits()
    {
        Assert.Equal("1404/05/15", PersianDate.ToDisplay(Day));
    }

    [Theory]
    [InlineData(2026, 3, 21, 1405, 1, 1)]    // Nowruz 1405
    [InlineData(2026, 5, 30, 1405, 3, 9)]
    [InlineData(2026, 8, 11, 1405, 5, 20)]
    [InlineData(2025, 8, 6, 1404, 5, 15)]
    [InlineData(2024, 3, 20, 1403, 1, 1)]    // leap-year Nowruz
    public void ToJalali_MatchesKnownGregorianDates(
        int gy, int gm, int gd, int jy, int jm, int jd)
    {
        var (y, m, d) = PersianDate.ToJalali(new DateTime(gy, gm, gd));

        Assert.Equal((jy, jm, jd), (y, m, d));
    }

    [Theory]
    [InlineData(1405, 1, 1, 2026, 3, 21)]
    [InlineData(1405, 5, 20, 2026, 8, 11)]
    public void FromJalali_RoundTripsBackToGregorian(
        int jy, int jm, int jd, int gy, int gm, int gd)
    {
        Assert.Equal(new DateTime(gy, gm, gd), PersianDate.FromJalali(jy, jm, jd));
    }

    [Fact]
    public void ToDisplayWithTime_AppendsTimeOfDay()
    {
        var text = Strip(PersianDate.ToDisplayWithTime(Day.AddHours(14).AddMinutes(32)));

        Assert.Equal("1404/05/15 14:32", text);
    }

    [Fact]
    public void ToDisplayWithTime_UsesEnglishDigitsAndA24HourClockUnderAPersianCulture()
    {
        var previous = CultureInfo.CurrentCulture;
        CultureInfo.CurrentCulture = new CultureInfo("fa-IR");
        try
        {
            var text = Strip(PersianDate.ToDisplayWithTime(Day.AddHours(21).AddMinutes(5)));

            Assert.Equal("1404/05/15 21:05", text);
        }
        finally
        {
            CultureInfo.CurrentCulture = previous;
        }
    }

    [Fact]
    public void ToDisplayWithTime_OmitsMidnight()
    {
        // Expenses saved before times were recorded sit at midnight; "00:00" would be a lie.
        Assert.Equal("1404/05/15", Strip(PersianDate.ToDisplayWithTime(Day)));
    }

    [Fact]
    public void ToDisplayWithTime_IsolatesTheRunSoRtlLinesDoNotSwapDateAndTime()
    {
        var text = PersianDate.ToDisplayWithTime(Day.AddHours(9));

        Assert.StartsWith("⁦", text);
        Assert.EndsWith("⁩", text);
    }

    /// <summary>Drops the directional isolates so assertions read as plain text.</summary>
    private static string Strip(string text) => text.Trim('⁦', '⁩');
}
