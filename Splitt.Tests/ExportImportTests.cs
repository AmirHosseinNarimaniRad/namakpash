using Splitt.Core.Data;
using Splitt.Core.Export;
using Splitt.Core.Models;
using Splitt.Core.Services;

namespace Splitt.Tests;

public class ExportImportTests
{
    private static (Trip, List<Participant>, List<Expense>, List<ExpenseShare>) SampleTrip()
    {
        var trip = new Trip { Id = 7, Name = "شمال", CreatedAtUtc = new DateTime(2026, 8, 1, 0, 0, 0, DateTimeKind.Utc) };
        var people = new List<Participant>
        {
            new() { Id = 1, TripId = 7, Name = "امیر" },
            new() { Id = 2, TripId = 7, Name = "سارا" },
        };
        var expenses = new List<Expense>
        {
            new() { Id = 10, TripId = 7, Description = "بنزین", Amount = 450_000, PaidById = 1, DateUtc = DateTime.UtcNow, IsSettlement = false },
            new() { Id = 11, TripId = 7, Description = "تسویه", Amount = 225_000, PaidById = 2, DateUtc = DateTime.UtcNow, IsSettlement = true },
        };
        var shares = new List<ExpenseShare>
        {
            new() { ExpenseId = 10, ParticipantId = 1, Share = 225_000 },
            new() { ExpenseId = 10, ParticipantId = 2, Share = 225_000 },
            new() { ExpenseId = 11, ParticipantId = 1, Share = 225_000 },
        };
        return (trip, people, expenses, shares);
    }

    [Fact]
    public void RoundTrip_PreservesEverything()
    {
        var (trip, people, expenses, shares) = SampleTrip();

        var json = TripExporter.ToJson(trip, people, expenses, shares);
        var dto = TripExporter.FromJson(json);

        Assert.Equal("شمال", dto.TripName);
        Assert.Equal(2, dto.Participants.Count);
        Assert.Equal(2, dto.Expenses.Count);
        Assert.Equal("450000", dto.Expenses[0].Amount);
        Assert.True(dto.Expenses[1].IsSettlement);
        Assert.Equal(2, dto.Expenses[0].Shares.Count);
        Assert.Equal("225000", dto.Expenses[0].Shares[0].Share);
    }

    [Fact]
    public void ImportedTrip_YieldsIdenticalBalances()
    {
        var (trip, people, expenses, shares) = SampleTrip();
        var originalNet = BalanceCalculator.ComputeNet(people, expenses, shares);

        var dto = TripExporter.FromJson(TripExporter.ToJson(trip, people, expenses, shares));

        // Simulate the import remap with arbitrary new ids.
        var idMap = dto.Participants.Select((p, i) => (p.Id, NewId: 100 + i)).ToDictionary(x => x.Id, x => x.NewId);
        var newPeople = dto.Participants.Select(p => new Participant { Id = idMap[p.Id], Name = p.Name }).ToList();
        var newExpenses = new List<Expense>();
        var newShares = new List<ExpenseShare>();
        int nextExpenseId = 500;
        foreach (var e in dto.Expenses)
        {
            int id = nextExpenseId++;
            newExpenses.Add(new Expense { Id = id, AmountRaw = e.Amount, PaidById = idMap[e.PaidById], IsSettlement = e.IsSettlement });
            newShares.AddRange(e.Shares.Select(s => new ExpenseShare { ExpenseId = id, ParticipantId = idMap[s.ParticipantId], ShareRaw = s.Share }));
        }

        var importedNet = BalanceCalculator.ComputeNet(newPeople, newExpenses, newShares);

        foreach (var p in people)
            Assert.Equal(originalNet[p.Id], importedNet[idMap[p.Id]]);
    }

    [Fact]
    public void InvalidJson_Throws()
    {
        Assert.ThrowsAny<Exception>(() => TripExporter.FromJson("not json"));
        Assert.Throws<InvalidDataException>(() => TripExporter.FromJson("""{"TripName":"x","Participants":[]}"""));
    }

    [Fact]
    public void InconsistentIds_Throw()
    {
        var json = """
        {
          "SchemaVersion": 1,
          "TripName": "x",
          "Participants": [{"Id": 1, "Name": "a"}],
          "Expenses": [{"Description": "d", "Amount": "10", "PaidById": 99, "Shares": []}]
        }
        """;
        Assert.Throws<InvalidDataException>(() => TripExporter.FromJson(json));
    }

    [Fact]
    public async Task Database_ImportRoundTrip_PreservesBalances()
    {
        var (trip, people, expenses, shares) = SampleTrip();
        var json = TripExporter.ToJson(trip, people, expenses, shares);

        var dbPath = Path.Combine(Path.GetTempPath(), $"splitt-test-{Guid.NewGuid():N}.db3");
        try
        {
            var db = new SplittDatabase(dbPath);
            await db.InitializeAsync();

            var imported = await db.ImportTripAsync(TripExporter.FromJson(json));

            var newPeople = await db.GetParticipantsAsync(imported.Id);
            var newExpenses = await db.GetExpensesAsync(imported.Id);
            var newShares = await db.GetSharesForTripAsync(imported.Id);

            var net = BalanceCalculator.ComputeNet(newPeople, newExpenses, newShares);
            Assert.Equal(2, net.Count);
            Assert.All(net.Values, v => Assert.Equal(0m, v)); // sample trip is fully settled
            Assert.Equal(450_000m + 225_000m, newExpenses.Sum(e => e.Amount));
        }
        finally
        {
            File.Delete(dbPath);
        }
    }
}
