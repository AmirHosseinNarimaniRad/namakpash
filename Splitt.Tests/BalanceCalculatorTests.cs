using Splitt.Core.Models;
using Splitt.Core.Services;

namespace Splitt.Tests;

public class BalanceCalculatorTests
{
    private static readonly List<Participant> People =
    [
        new() { Id = 1, TripId = 1, Name = "امیر" },
        new() { Id = 2, TripId = 1, Name = "سارا" },
        new() { Id = 3, TripId = 1, Name = "رضا" },
    ];

    private static (Expense, List<ExpenseShare>) MakeExpense(
        int id, int paidBy, decimal amount, params (int participantId, decimal share)[] shares)
    {
        var expense = new Expense { Id = id, TripId = 1, PaidById = paidBy, Amount = amount };
        var shareList = shares
            .Select(s => new ExpenseShare { ExpenseId = id, ParticipantId = s.participantId, Share = s.share })
            .ToList();
        return (expense, shareList);
    }

    [Fact]
    public void NoExpenses_AllBalancesZero()
    {
        var net = BalanceCalculator.ComputeNet(People, [], []);

        Assert.All(net.Values, v => Assert.Equal(0m, v));
        Assert.Equal(3, net.Count);
    }

    [Fact]
    public void SingleEqualExpense_PayerIsCreditor()
    {
        // امیر pays 90,000 split equally 3 ways.
        var (e, s) = MakeExpense(1, paidBy: 1, amount: 90_000, (1, 30_000), (2, 30_000), (3, 30_000));

        var net = BalanceCalculator.ComputeNet(People, [e], s);

        Assert.Equal(60_000m, net[1]);
        Assert.Equal(-30_000m, net[2]);
        Assert.Equal(-30_000m, net[3]);
        Assert.Equal(0m, net.Values.Sum());
    }

    [Fact]
    public void MultipleExpenses_NetsAccumulateAndSumToZero()
    {
        var (e1, s1) = MakeExpense(1, paidBy: 1, amount: 100, (1, 34), (2, 33), (3, 33));
        var (e2, s2) = MakeExpense(2, paidBy: 2, amount: 60, (2, 30), (3, 30));
        var (e3, s3) = MakeExpense(3, paidBy: 3, amount: 10, (1, 10));

        var net = BalanceCalculator.ComputeNet(
            People, [e1, e2, e3], s1.Concat(s2).Concat(s3));

        Assert.Equal(100 - 34 - 10, net[1]);
        Assert.Equal(60 - 33 - 30, net[2]);
        Assert.Equal(10 - 33 - 30, net[3]);
        Assert.Equal(0m, net.Values.Sum());
    }

    [Fact]
    public void DeletingExpense_RestoresPreviousBalances()
    {
        var (e1, s1) = MakeExpense(1, paidBy: 1, amount: 100, (1, 50), (2, 50));
        var (e2, s2) = MakeExpense(2, paidBy: 2, amount: 40, (1, 20), (2, 20));

        var before = BalanceCalculator.ComputeNet(People, [e1], s1);
        var withBoth = BalanceCalculator.ComputeNet(People, [e1, e2], s1.Concat(s2));
        var afterDelete = BalanceCalculator.ComputeNet(People, [e1], s1);

        Assert.NotEqual(before[2], withBoth[2]);
        Assert.Equal(before[1], afterDelete[1]);
        Assert.Equal(before[2], afterDelete[2]);
    }

    [Fact]
    public void EditingExpense_RecomputesFromScratch()
    {
        var (original, originalShares) = MakeExpense(1, paidBy: 1, amount: 100, (1, 50), (2, 50));
        var (edited, editedShares) = MakeExpense(1, paidBy: 2, amount: 200, (1, 120), (2, 80));

        var netOriginal = BalanceCalculator.ComputeNet(People, [original], originalShares);
        var netEdited = BalanceCalculator.ComputeNet(People, [edited], editedShares);

        Assert.Equal(50m, netOriginal[1]);
        Assert.Equal(-120m, netEdited[1]);
        Assert.Equal(120m, netEdited[2]);
    }

    [Fact]
    public void Settlement_AsExpense_ZeroesTheDebt()
    {
        // سارا owes امیر 30,000 after e1.
        var (e1, s1) = MakeExpense(1, paidBy: 1, amount: 60_000, (1, 30_000), (2, 30_000));

        // Settlement: سارا (debtor) pays; امیر (creditor) holds the whole share.
        var (settle, settleShares) = MakeExpense(2, paidBy: 2, amount: 30_000, (1, 30_000));
        settle.IsSettlement = true;

        var net = BalanceCalculator.ComputeNet(People, [e1, settle], s1.Concat(settleShares));

        Assert.Equal(0m, net[1]);
        Assert.Equal(0m, net[2]);
    }

    [Fact]
    public void CustomUnequalSplit_IsRespected()
    {
        var (e, s) = MakeExpense(1, paidBy: 3, amount: 100_000, (1, 70_000), (2, 30_000));

        var net = BalanceCalculator.ComputeNet(People, [e], s);

        Assert.Equal(-70_000m, net[1]);
        Assert.Equal(-30_000m, net[2]);
        Assert.Equal(100_000m, net[3]);
    }
}
