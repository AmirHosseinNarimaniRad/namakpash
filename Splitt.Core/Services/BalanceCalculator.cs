using Splitt.Core.Models;

namespace Splitt.Core.Services;

public static class BalanceCalculator
{
    /// <summary>
    /// Derives each participant's net balance from the expense list.
    /// net = paid − owed. Positive: creditor (others owe them), negative: debtor.
    /// Settlements are ordinary expenses here, so recording one shifts balances automatically.
    /// Balances are always computed, never stored.
    /// </summary>
    public static Dictionary<int, decimal> ComputeNet(
        IEnumerable<Participant> participants,
        IEnumerable<Expense> expenses,
        IEnumerable<ExpenseShare> shares)
    {
        var net = participants.ToDictionary(p => p.Id, _ => 0m);

        foreach (var expense in expenses)
        {
            if (net.ContainsKey(expense.PaidById))
                net[expense.PaidById] += expense.Amount;
        }

        foreach (var share in shares)
        {
            if (net.ContainsKey(share.ParticipantId))
                net[share.ParticipantId] -= share.Share;
        }

        return net;
    }
}
