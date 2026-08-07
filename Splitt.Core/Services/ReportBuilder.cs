using Splitt.Core.Models;

namespace Splitt.Core.Services;

/// <summary>One expense line inside a person's report (what they paid or what their share was).</summary>
public sealed record ReportItem(string Description, DateTime DateUtc, decimal Amount);

/// <summary>
/// Everything the report shows about one participant.
/// Paid/Owed cover real expenses only; settlements are tracked separately so
/// "paid" keeps meaning "spent on the trip". Net comes from BalanceCalculator
/// (settlements included), so the report can never disagree with the balances tab.
/// Because settlements shift Net but not Paid/Owed: Net = Paid − Owed + SettledPaid − SettledReceived.
/// </summary>
public sealed record PersonReport(
    int ParticipantId,
    string Name,
    decimal Paid,
    decimal Owed,
    decimal SettledPaid,
    decimal SettledReceived,
    decimal Net,
    IReadOnlyList<ReportItem> PaidItems,
    IReadOnlyList<ReportItem> ShareItems);

public sealed record TripReport(
    decimal Total,
    decimal AveragePerPerson,
    IReadOnlyList<PersonReport> People);

public static class ReportBuilder
{
    /// <summary>
    /// Derives the whole-trip report. Pure: nothing is stored, order of the
    /// input lists does not matter — items come out chronological (date, then id).
    /// </summary>
    public static TripReport Build(
        IReadOnlyList<Participant> participants,
        IReadOnlyList<Expense> expenses,
        IReadOnlyList<ExpenseShare> shares)
    {
        var net = BalanceCalculator.ComputeNet(participants, expenses, shares);
        var expenseById = expenses.ToDictionary(e => e.Id);
        var chronological = expenses.OrderBy(e => e.DateUtc).ThenBy(e => e.Id).ToList();
        var sharesByParticipant = shares.ToLookup(s => s.ParticipantId);

        var people = new List<PersonReport>(participants.Count);
        foreach (var p in participants)
        {
            var paidItems = chronological
                .Where(e => !e.IsSettlement && e.PaidById == p.Id)
                .Select(e => new ReportItem(e.Description, e.DateUtc, e.Amount))
                .ToList();

            var shareItems = sharesByParticipant[p.Id]
                .Select(s => (Share: s, Expense: expenseById.GetValueOrDefault(s.ExpenseId)))
                .Where(x => x.Expense is { IsSettlement: false })
                .OrderBy(x => x.Expense!.DateUtc).ThenBy(x => x.Expense!.Id)
                .Select(x => new ReportItem(x.Expense!.Description, x.Expense!.DateUtc, x.Share.Share))
                .ToList();

            var settledPaid = expenses
                .Where(e => e.IsSettlement && e.PaidById == p.Id)
                .Sum(e => e.Amount);

            var settledReceived = sharesByParticipant[p.Id]
                .Where(s => expenseById.GetValueOrDefault(s.ExpenseId) is { IsSettlement: true })
                .Sum(s => s.Share);

            people.Add(new PersonReport(
                p.Id,
                p.Name,
                Paid: paidItems.Sum(i => i.Amount),
                Owed: shareItems.Sum(i => i.Amount),
                SettledPaid: settledPaid,
                SettledReceived: settledReceived,
                Net: net.GetValueOrDefault(p.Id),
                PaidItems: paidItems,
                ShareItems: shareItems));
        }

        var total = people.Sum(p => p.Paid);
        var average = participants.Count == 0
            ? 0m
            : Math.Round(total / participants.Count, 0, MidpointRounding.AwayFromZero);

        return new TripReport(total, average, people);
    }
}
