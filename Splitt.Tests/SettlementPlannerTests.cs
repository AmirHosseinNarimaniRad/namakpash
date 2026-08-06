using Splitt.Core.Services;

namespace Splitt.Tests;

public class SettlementPlannerTests
{
    private static void AssertPlanSettles(Dictionary<int, decimal> balances)
    {
        var plan = SettlementPlanner.Plan(balances);
        var applied = new Dictionary<int, decimal>(balances);

        foreach (var t in plan)
        {
            Assert.True(t.Amount > 0);
            applied[t.FromParticipantId] += t.Amount;
            applied[t.ToParticipantId] -= t.Amount;
        }

        Assert.All(applied.Values, v => Assert.Equal(0m, v));
    }

    [Fact]
    public void AllZero_ProducesNoTransactions()
    {
        var plan = SettlementPlanner.Plan(new Dictionary<int, decimal> { [1] = 0, [2] = 0 });
        Assert.Empty(plan);
    }

    [Fact]
    public void SimplePair_OneTransaction()
    {
        var plan = SettlementPlanner.Plan(new Dictionary<int, decimal> { [1] = 50_000, [2] = -50_000 });

        var t = Assert.Single(plan);
        Assert.Equal(2, t.FromParticipantId);
        Assert.Equal(1, t.ToParticipantId);
        Assert.Equal(50_000m, t.Amount);
    }

    [Fact]
    public void LargestDebtorPaysLargestCreditorFirst()
    {
        var plan = SettlementPlanner.Plan(new Dictionary<int, decimal>
        {
            [1] = 70, [2] = 30, [3] = -80, [4] = -20,
        });

        Assert.Equal(3, plan[0].FromParticipantId);
        Assert.Equal(1, plan[0].ToParticipantId);
        Assert.Equal(70m, plan[0].Amount);
    }

    [Fact]
    public void PlanFullySettles_VariousScenarios()
    {
        AssertPlanSettles(new Dictionary<int, decimal> { [1] = 100, [2] = -60, [3] = -40 });
        AssertPlanSettles(new Dictionary<int, decimal> { [1] = 34, [2] = -33, [3] = -1 });
        AssertPlanSettles(new Dictionary<int, decimal>
        {
            [1] = 123_456, [2] = -654, [3] = -122_802, [4] = 0,
        });
        AssertPlanSettles(new Dictionary<int, decimal>
        {
            [1] = 5, [2] = 5, [3] = 5, [4] = -5, [5] = -5, [6] = -5,
        });
    }

    [Fact]
    public void ProducesAtMostNMinusOneTransactions()
    {
        var balances = new Dictionary<int, decimal>
        {
            [1] = 90, [2] = 10, [3] = -25, [4] = -25, [5] = -25, [6] = -25,
        };

        var plan = SettlementPlanner.Plan(balances);

        Assert.True(plan.Count <= balances.Count - 1);
        AssertPlanSettles(balances);
    }

    [Fact]
    public void Deterministic_TiesBreakOnLowerId()
    {
        var plan1 = SettlementPlanner.Plan(new Dictionary<int, decimal> { [3] = -10, [2] = 10, [1] = 0 });
        var plan2 = SettlementPlanner.Plan(new Dictionary<int, decimal> { [1] = 0, [2] = 10, [3] = -10 });

        Assert.Equal(plan1, plan2);
    }
}
