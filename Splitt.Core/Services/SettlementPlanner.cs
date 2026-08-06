namespace Splitt.Core.Services;

public sealed record SettlementSuggestion(int FromParticipantId, int ToParticipantId, decimal Amount);

public static class SettlementPlanner
{
    /// <summary>
    /// Greedy debt simplification: the largest debtor pays the largest creditor,
    /// repeated until everyone is settled. Ties break on the lower participant id,
    /// so the output is deterministic. Produces at most n−1 transactions.
    /// </summary>
    public static List<SettlementSuggestion> Plan(IReadOnlyDictionary<int, decimal> netBalances)
    {
        var balances = netBalances
            .Where(kv => kv.Value != 0)
            .ToDictionary(kv => kv.Key, kv => kv.Value);

        var result = new List<SettlementSuggestion>();

        while (true)
        {
            int creditorId = 0, debtorId = 0;
            decimal maxCredit = 0, maxDebt = 0;

            foreach (var (id, value) in balances)
            {
                if (value > maxCredit || (value == maxCredit && value > 0 && id < creditorId))
                {
                    maxCredit = value;
                    creditorId = id;
                }
                if (value < -maxDebt || (value == -maxDebt && value < 0 && id < debtorId))
                {
                    maxDebt = -value;
                    debtorId = id;
                }
            }

            if (maxCredit == 0 || maxDebt == 0)
                break;

            decimal amount = Math.Min(maxCredit, maxDebt);
            result.Add(new SettlementSuggestion(debtorId, creditorId, amount));

            balances[creditorId] -= amount;
            balances[debtorId] += amount;
            if (balances[creditorId] == 0) balances.Remove(creditorId);
            if (balances[debtorId] == 0) balances.Remove(debtorId);
        }

        return result;
    }
}
