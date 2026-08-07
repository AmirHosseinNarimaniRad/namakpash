using Splitt.Core.Models;
using Splitt.Core.Services;

namespace Splitt.Tests;

public class ReportBuilderTests
{
    private static readonly List<Participant> People =
    [
        new() { Id = 1, TripId = 1, Name = "امیر" },
        new() { Id = 2, TripId = 1, Name = "سارا" },
        new() { Id = 3, TripId = 1, Name = "رضا" },
    ];

    private static (Expense, List<ExpenseShare>) MakeExpense(
        int id, int paidBy, decimal amount, DateTime dateUtc, bool isSettlement,
        params (int participantId, decimal share)[] shares)
    {
        var expense = new Expense
        {
            Id = id, TripId = 1, PaidById = paidBy, Amount = amount,
            DateUtc = dateUtc, IsSettlement = isSettlement,
            Description = isSettlement ? "تسویه" : $"هزینه {id}",
        };
        var shareList = shares
            .Select(s => new ExpenseShare { ExpenseId = id, ParticipantId = s.participantId, Share = s.share })
            .ToList();
        return (expense, shareList);
    }

    private static readonly DateTime Day1 = new(2026, 8, 1, 12, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime Day2 = new(2026, 8, 2, 12, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime Day3 = new(2026, 8, 3, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void EmptyTrip_ZeroTotalsAndOnePersonRowEach()
    {
        var report = ReportBuilder.Build(People, [], []);

        Assert.Equal(0m, report.Total);
        Assert.Equal(0m, report.AveragePerPerson);
        Assert.Equal(3, report.People.Count);
        Assert.All(report.People, p =>
        {
            Assert.Equal(0m, p.Paid);
            Assert.Equal(0m, p.Owed);
            Assert.Equal(0m, p.Net);
            Assert.Empty(p.PaidItems);
            Assert.Empty(p.ShareItems);
        });
    }

    [Fact]
    public void PaidAndOwed_SumPerPerson()
    {
        var (e1, s1) = MakeExpense(1, paidBy: 1, 90_000m, Day1, false, (1, 30_000m), (2, 30_000m), (3, 30_000m));
        var (e2, s2) = MakeExpense(2, paidBy: 2, 60_000m, Day2, false, (1, 30_000m), (2, 30_000m));

        var report = ReportBuilder.Build(People, [e1, e2], [.. s1, .. s2]);

        var amir = report.People.Single(p => p.ParticipantId == 1);
        Assert.Equal(90_000m, amir.Paid);
        Assert.Equal(60_000m, amir.Owed);
        Assert.Equal(30_000m, amir.Net);

        var reza = report.People.Single(p => p.ParticipantId == 3);
        Assert.Equal(0m, reza.Paid);
        Assert.Equal(30_000m, reza.Owed);
        Assert.Equal(-30_000m, reza.Net);

        Assert.Equal(150_000m, report.Total);
        Assert.Equal(50_000m, report.AveragePerPerson);
    }

    [Fact]
    public void NetAlwaysMatchesBalanceCalculator()
    {
        var (e1, s1) = MakeExpense(1, paidBy: 1, 100_000m, Day1, false, (1, 33_334m), (2, 33_333m), (3, 33_333m));
        var (e2, s2) = MakeExpense(2, paidBy: 3, 33_333m, Day2, true, (1, 33_333m));
        List<Expense> expenses = [e1, e2];
        List<ExpenseShare> shares = [.. s1, .. s2];

        var report = ReportBuilder.Build(People, expenses, shares);
        var net = BalanceCalculator.ComputeNet(People, expenses, shares);

        Assert.All(report.People, p => Assert.Equal(net[p.ParticipantId], p.Net));
    }

    [Fact]
    public void Settlements_ExcludedFromPaidOwedAndTotal_TrackedSeparately()
    {
        var (e1, s1) = MakeExpense(1, paidBy: 1, 100_000m, Day1, false, (1, 50_000m), (2, 50_000m));
        var (e2, s2) = MakeExpense(2, paidBy: 2, 50_000m, Day2, true, (1, 50_000m));

        var report = ReportBuilder.Build(People, [e1, e2], [.. s1, .. s2]);

        var amir = report.People.Single(p => p.ParticipantId == 1);
        var sara = report.People.Single(p => p.ParticipantId == 2);

        Assert.Equal(100_000m, report.Total);          // settlement not counted as spending
        Assert.Equal(100_000m, amir.Paid);
        Assert.Equal(50_000m, sara.Owed);
        Assert.Equal(0m, sara.SettledReceived);
        Assert.Equal(50_000m, sara.SettledPaid);
        Assert.Equal(50_000m, amir.SettledReceived);
        Assert.Equal(0m, amir.Net);                    // fully settled
        Assert.Equal(0m, sara.Net);
        Assert.Single(amir.PaidItems);                 // settlement produces no items
        Assert.Empty(sara.PaidItems);
    }

    [Fact]
    public void NetIdentity_HoldsWithSettlements()
    {
        var (e1, s1) = MakeExpense(1, paidBy: 1, 100_000m, Day1, false, (1, 33_334m), (2, 33_333m), (3, 33_333m));
        var (e2, s2) = MakeExpense(2, paidBy: 2, 20_000m, Day2, true, (1, 20_000m));

        var report = ReportBuilder.Build(People, [e1, e2], [.. s1, .. s2]);

        Assert.All(report.People, p =>
            Assert.Equal(p.Net, p.Paid - p.Owed + p.SettledPaid - p.SettledReceived));
    }

    [Fact]
    public void Items_ChronologicalRegardlessOfInputOrder()
    {
        var (e1, s1) = MakeExpense(1, paidBy: 1, 10_000m, Day3, false, (1, 10_000m));
        var (e2, s2) = MakeExpense(2, paidBy: 1, 20_000m, Day1, false, (1, 20_000m));
        var (e3, s3) = MakeExpense(3, paidBy: 1, 30_000m, Day2, false, (1, 30_000m));

        // Input deliberately out of order (DB returns newest first).
        var report = ReportBuilder.Build(People, [e1, e3, e2], [.. s1, .. s2, .. s3]);

        var amir = report.People.Single(p => p.ParticipantId == 1);
        Assert.Equal([20_000m, 30_000m, 10_000m], amir.PaidItems.Select(i => i.Amount));
        Assert.Equal([20_000m, 30_000m, 10_000m], amir.ShareItems.Select(i => i.Amount));
    }

    [Fact]
    public void Average_RoundsToWholeToman()
    {
        var (e1, s1) = MakeExpense(1, paidBy: 1, 100_000m, Day1, false, (1, 33_334m), (2, 33_333m), (3, 33_333m));

        var report = ReportBuilder.Build(People, [e1], s1);

        Assert.Equal(33_333m, report.AveragePerPerson); // 100000/3 = 33333.33… → 33333
    }
}
