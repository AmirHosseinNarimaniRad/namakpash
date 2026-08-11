using SQLite;
using Splitt.Core.Export;
using Splitt.Core.Models;

namespace Splitt.Core.Data;

public class SplittDatabase
{
    private readonly SQLiteAsyncConnection _db;

    public SplittDatabase(string databasePath)
    {
        _db = new SQLiteAsyncConnection(
            databasePath,
            SQLiteOpenFlags.ReadWrite | SQLiteOpenFlags.Create | SQLiteOpenFlags.SharedCache);
    }

    public async Task InitializeAsync()
    {
        await _db.CreateTableAsync<Trip>();
        await _db.CreateTableAsync<Participant>();
        await _db.CreateTableAsync<Expense>();
        await _db.CreateTableAsync<ExpenseShare>();
    }

    // ---- Trips ----

    public Task<List<Trip>> GetTripsAsync() =>
        _db.Table<Trip>().OrderByDescending(t => t.CreatedAtUtc).ToListAsync();

    public Task<Trip?> GetTripAsync(int id) =>
        _db.Table<Trip>().Where(t => t.Id == id).FirstOrDefaultAsync()!;

    public async Task<Trip> CreateTripAsync(string name, IEnumerable<string> participantNames)
    {
        var trip = new Trip { Name = name, CreatedAtUtc = DateTime.UtcNow };
        await _db.RunInTransactionAsync(conn =>
        {
            conn.Insert(trip);
            foreach (var participantName in participantNames)
                conn.Insert(new Participant { TripId = trip.Id, Name = participantName });
        });
        return trip;
    }

    public Task UpdateTripAsync(Trip trip) => _db.UpdateAsync(trip);

    public Task DeleteTripAsync(int tripId) =>
        _db.RunInTransactionAsync(conn =>
        {
            var expenseIds = conn.Table<Expense>().Where(e => e.TripId == tripId)
                .ToList().Select(e => e.Id).ToList();
            foreach (var expenseId in expenseIds)
                conn.Table<ExpenseShare>().Where(s => s.ExpenseId == expenseId).Delete();
            conn.Table<Expense>().Where(e => e.TripId == tripId).Delete();
            conn.Table<Participant>().Where(p => p.TripId == tripId).Delete();
            conn.Delete<Trip>(tripId);
        });

    // ---- Participants ----

    public Task<List<Participant>> GetParticipantsAsync(int tripId) =>
        _db.Table<Participant>().Where(p => p.TripId == tripId).OrderBy(p => p.Id).ToListAsync();

    public Task<int> AddParticipantAsync(Participant participant) => _db.InsertAsync(participant);

    public Task UpdateParticipantAsync(Participant participant) => _db.UpdateAsync(participant);

    /// <summary>True if the participant appears in any expense (as payer or share holder).</summary>
    public async Task<bool> ParticipantHasActivityAsync(int participantId)
    {
        var paid = await _db.Table<Expense>().Where(e => e.PaidById == participantId).CountAsync();
        if (paid > 0) return true;
        var shares = await _db.Table<ExpenseShare>().Where(s => s.ParticipantId == participantId).CountAsync();
        return shares > 0;
    }

    public Task DeleteParticipantAsync(int participantId) => _db.DeleteAsync<Participant>(participantId);

    // ---- Expenses ----

    /// <summary>
    /// Newest first. The id breaks ties so expenses that share a timestamp — anything
    /// saved before times were recorded, and imports — still come back newest first
    /// instead of in whatever order SQLite happens to return them.
    /// </summary>
    public Task<List<Expense>> GetExpensesAsync(int tripId) =>
        _db.Table<Expense>().Where(e => e.TripId == tripId)
            .OrderByDescending(e => e.DateUtc).ThenByDescending(e => e.Id).ToListAsync();

    public async Task<List<ExpenseShare>> GetSharesForTripAsync(int tripId)
    {
        return await _db.QueryAsync<ExpenseShare>(
            "SELECT s.* FROM ExpenseShare s INNER JOIN Expense e ON s.ExpenseId = e.Id WHERE e.TripId = ?",
            tripId);
    }

    public Task<List<ExpenseShare>> GetSharesForExpenseAsync(int expenseId) =>
        _db.Table<ExpenseShare>().Where(s => s.ExpenseId == expenseId).ToListAsync();

    /// <summary>Inserts or updates an expense and replaces its shares atomically.</summary>
    public Task SaveExpenseAsync(Expense expense, IReadOnlyList<ExpenseShare> shares) =>
        _db.RunInTransactionAsync(conn =>
        {
            if (expense.Id == 0)
                conn.Insert(expense);
            else
            {
                conn.Update(expense);
                conn.Table<ExpenseShare>().Where(s => s.ExpenseId == expense.Id).Delete();
            }
            foreach (var share in shares)
            {
                share.Id = 0;
                share.ExpenseId = expense.Id;
                conn.Insert(share);
            }
        });

    public Task DeleteExpenseAsync(int expenseId) =>
        _db.RunInTransactionAsync(conn =>
        {
            conn.Table<ExpenseShare>().Where(s => s.ExpenseId == expenseId).Delete();
            conn.Delete<Expense>(expenseId);
        });

    // ---- Import ----

    /// <summary>Imports a backup as a new trip, remapping all participant/expense ids.</summary>
    public async Task<Trip> ImportTripAsync(TripExportDto dto)
    {
        var trip = new Trip { Name = dto.TripName, CreatedAtUtc = DateTime.UtcNow };
        await _db.RunInTransactionAsync(conn =>
        {
            conn.Insert(trip);

            var idMap = new Dictionary<int, int>();
            foreach (var p in dto.Participants)
            {
                var participant = new Participant { TripId = trip.Id, Name = p.Name };
                conn.Insert(participant);
                idMap[p.Id] = participant.Id;
            }

            foreach (var e in dto.Expenses)
            {
                var expense = new Expense
                {
                    TripId = trip.Id,
                    Description = e.Description,
                    AmountRaw = e.Amount,
                    PaidById = idMap[e.PaidById],
                    DateUtc = e.DateUtc,
                    IsSettlement = e.IsSettlement,
                };
                conn.Insert(expense);

                foreach (var s in e.Shares)
                {
                    conn.Insert(new ExpenseShare
                    {
                        ExpenseId = expense.Id,
                        ParticipantId = idMap[s.ParticipantId],
                        ShareRaw = s.Share,
                    });
                }
            }
        });
        return trip;
    }
}
