using System.Globalization;
using System.Runtime.InteropServices;
using Splitt.Core.Data;
using Splitt.Core.Helpers;
using Splitt.Core.Services;

namespace Splitt.Wasm.Spike;

/// <summary>
/// The three things that decide whether Splitt.Core can live in a browser at all:
/// decimal money arithmetic, the Persian calendar (which needs ICU data, not just a culture),
/// and SQLite (which needs a native library the WASM build may or may not link in).
/// Each probe reports what actually happened rather than asserting, because a failure here
/// is a finding, not a bug.
/// </summary>
public static class EnvironmentProbe
{
    public record Probe(string Name, bool Ok, string Detail);

    public static async Task<List<Probe>> RunAllAsync()
    {
        var probes = new List<Probe>
        {
            Runtime(),
            Globalization(),
            PersianCalendarWorks(),
            DecimalIsRealDecimal(),
            AmountTextRoundTrip(),
            EqualSplitSumsExactly()
        };

        probes.Add(await SqliteOpensAsync());
        return probes;
    }

    static Probe Runtime() => new(
        "Runtime",
        true,
        $"{RuntimeInformation.FrameworkDescription} · {RuntimeInformation.RuntimeIdentifier} · " +
        $"OS: {RuntimeInformation.OSDescription}");

    static Probe Globalization()
    {
        // Invariant mode is the failure the plan worried about: cultures still "work" but every
        // one of them is the invariant culture, which has no Persian calendar behind it.
        AppContext.TryGetSwitch("System.Globalization.Invariant", out var invariant);
        try
        {
            var fa = new CultureInfo("fa-IR");
            var detail = $"invariant mode: {invariant} · fa-IR resolves to \"{fa.EnglishName}\"";
            return new Probe("Globalization / ICU", !invariant && fa.EnglishName.Contains("Persian"), detail);
        }
        catch (Exception ex)
        {
            return new Probe("Globalization / ICU", false, $"invariant mode: {invariant} · {ex.GetType().Name}: {ex.Message}");
        }
    }

    static Probe PersianCalendarWorks()
    {
        try
        {
            // 2026-08-23 Gregorian is 1405/06/01 Jalali.
            var date = new DateTime(2026, 8, 23);
            var shown = PersianDate.ToDisplay(date);
            var (y, m, d) = PersianDate.ToJalali(date);
            var back = PersianDate.FromJalali(y, m, d);
            var ok = shown == "1405/06/01" && back.Date == date.Date;
            return new Probe("PersianCalendar", ok, $"{date:yyyy-MM-dd} → \"{shown}\" → back to {back:yyyy-MM-dd}");
        }
        catch (Exception ex)
        {
            return new Probe("PersianCalendar", false, $"{ex.GetType().Name}: {ex.Message}");
        }
    }

    static Probe DecimalIsRealDecimal()
    {
        // The whole argument for Blazor over TypeScript. A double would give 0.30000000000000004.
        decimal sum = 0.1m + 0.2m;
        var ok = sum == 0.3m;
        return new Probe("decimal arithmetic", ok, $"0.1 + 0.2 == 0.3 → {ok} (got {sum})");
    }

    static Probe AmountTextRoundTrip()
    {
        // Invariant #2: amounts persist as TEXT and are parsed with InvariantCulture.
        const decimal amount = 1234567.89m;
        var text = amount.ToString(CultureInfo.InvariantCulture);
        var parsed = decimal.Parse(text, CultureInfo.InvariantCulture);
        var formatted = MoneyFormat.FormatToman(1234567m);
        return new Probe("Amount TEXT round-trip", parsed == amount,
            $"{amount} → \"{text}\" → {parsed} · MoneyFormat.FormatToman(1234567) = \"{formatted}\"");
    }

    static Probe EqualSplitSumsExactly()
    {
        // Invariant #4: 100,000 / 3 → 33,334 + 33,333 + 33,333.
        var shares = EqualSplitter.Split(100_000m, 3);
        var ok = shares.Sum() == 100_000m && shares[0] == 33_334m;
        return new Probe("Equal split remainder", ok, string.Join(" + ", shares) + " = " + shares.Sum());
    }

    static async Task<Probe> SqliteOpensAsync()
    {
        // sqlite-net ships a browser-wasm build of e_sqlite3, but it is only linked when the
        // wasm-tools workload relinks natively. Without it this is expected to fail — and that
        // answer decides whether Data/ can be reused or has to be rewritten for IndexedDB/OPFS.
        var path = Path.Combine(Path.GetTempPath(), $"spike-{Guid.NewGuid():N}.db3");
        try
        {
            // The connection is lazy: nothing native is touched until a statement runs,
            // so the tables have to be created before this proves anything.
            var db = new SplittDatabase(path);
            await db.InitializeAsync();
            return new Probe("SQLite (sqlite-net)", true, $"created the schema at {path}");
        }
        catch (Exception ex)
        {
            return new Probe("SQLite (sqlite-net)", false, $"{ex.GetType().Name}: {ex.Message}");
        }
    }
}
