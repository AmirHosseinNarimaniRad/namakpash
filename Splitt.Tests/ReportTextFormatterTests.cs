using Splitt.Core.Helpers;
using Splitt.Core.Models;
using Splitt.Core.Services;

namespace Splitt.Tests;

public class ReportTextFormatterTests
{
    private static readonly List<Participant> People =
    [
        new() { Id = 1, TripId = 1, Name = "امیر" },
        new() { Id = 2, TripId = 1, Name = "سارا" },
    ];

    private static readonly DateTime Day1 = new(2026, 8, 1, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void FullReport_ContainsAllSections()
    {
        var expense = new Expense
        {
            Id = 1, TripId = 1, PaidById = 1, Amount = 100_000m,
            DateUtc = Day1, Description = "شام رستوران",
        };
        var shares = new List<ExpenseShare>
        {
            new() { ExpenseId = 1, ParticipantId = 1, Share = 50_000m },
            new() { ExpenseId = 1, ParticipantId = 2, Share = 50_000m },
        };

        var text = ReportTextFormatter.Format("شمال", People, [expense], shares);

        Assert.Contains("گزارش «شمال»", text);
        Assert.Contains("جمع هزینه‌ها: 100,000 تومان", text);
        Assert.Contains("میانگین هر نفر: 50,000 تومان", text);
        Assert.Contains("امیر — پرداخت: 100,000 · سهم: 50,000 · طلبکار: 50,000", text);
        Assert.Contains("سارا — پرداخت: 0 · سهم: 50,000 · بدهکار: 50,000", text);
        Assert.Contains("ریز هزینه‌ها", text);
        Assert.Contains("شام رستوران", text);
        Assert.Contains("پرداخت: امیر", text);
        Assert.Contains("پیشنهاد تسویه", text);
        Assert.Contains("سارا به امیر — 50,000 تومان", text);
        // English digits only — Persian-Indic digits are not wanted anywhere.
        Assert.DoesNotContain('۰', text);
        Assert.DoesNotContain('۱', text);
    }

    [Fact]
    public void SettledTrip_ListsSettlementAndOmitsSuggestions()
    {
        var expense = new Expense
        {
            Id = 1, TripId = 1, PaidById = 1, Amount = 100_000m,
            DateUtc = Day1, Description = "شام",
        };
        var settlement = new Expense
        {
            Id = 2, TripId = 1, PaidById = 2, Amount = 50_000m,
            DateUtc = Day1.AddDays(1), Description = "تسویه", IsSettlement = true,
        };
        var shares = new List<ExpenseShare>
        {
            new() { ExpenseId = 1, ParticipantId = 1, Share = 50_000m },
            new() { ExpenseId = 1, ParticipantId = 2, Share = 50_000m },
            new() { ExpenseId = 2, ParticipantId = 1, Share = 50_000m },
        };

        var text = ReportTextFormatter.Format("شمال", People, [expense, settlement], shares);

        Assert.Contains("تسویه‌های ثبت‌شده", text);
        Assert.Contains("سارا به امیر — 50,000 تومان", text);
        Assert.DoesNotContain("پیشنهاد تسویه", text);
        // Settlement is cashflow, not spending.
        Assert.Contains("جمع هزینه‌ها: 100,000 تومان", text);
    }

    [Fact]
    public void LatinNames_EveryLineStartsWithRtlMark()
    {
        // Regression: "Sara به Amir" without a leading RLM renders LTR (first strong
        // char is Latin) and an RTL reader sees it reversed as "Amir به Sara".
        List<Participant> people =
        [
            new() { Id = 1, TripId = 1, Name = "Amir" },
            new() { Id = 2, TripId = 1, Name = "Sara" },
        ];
        var expense = new Expense
        {
            Id = 1, TripId = 1, PaidById = 1, Amount = 100_000m,
            DateUtc = Day1, Description = "Villa",
        };
        var shares = new List<ExpenseShare>
        {
            new() { ExpenseId = 1, ParticipantId = 1, Share = 50_000m },
            new() { ExpenseId = 1, ParticipantId = 2, Share = 50_000m },
        };

        var text = ReportTextFormatter.Format("Shomal", people, [expense], shares);

        var lines = text.Split('\n', StringSplitOptions.RemoveEmptyEntries)
            .Select(l => l.TrimEnd('\r'))
            .Where(l => l.Length > 0);
        Assert.All(lines, l => Assert.StartsWith(Bidi.Rlm, l));
        Assert.Contains(Bidi.Rlm + "Sara به Amir — 50,000 تومان", text);
    }

    [Fact]
    public void EmptyDescription_FallsBackLikeTheUi()
    {
        var expense = new Expense
        {
            Id = 1, TripId = 1, PaidById = 1, Amount = 10_000m,
            DateUtc = Day1, Description = "",
        };
        var shares = new List<ExpenseShare> { new() { ExpenseId = 1, ParticipantId = 1, Share = 10_000m } };

        var text = ReportTextFormatter.Format("شمال", People, [expense], shares);

        Assert.Contains("بدون شرح", text);
    }
}
