using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Splitt.App.Helpers;
using Splitt.Core.Data;
using Splitt.Core.Export;
using Splitt.Core.Models;
using Splitt.Core.Services;

namespace Splitt.App.ViewModels;

public sealed record ExpenseRow(int Id, string Description, string Meta, string AmountText, bool IsSettlement);

public sealed record BalanceRow(string Name, string AmountText, string StatusText, bool IsCreditor, bool IsZero, double Fraction);

public sealed record SettlementRow(int FromId, int ToId, string Text, string AmountText, decimal Amount);

[QueryProperty(nameof(TripIdText), "tripId")]
public partial class TripDetailViewModel : ObservableObject
{
    private readonly SplittDatabase _db;
    private List<Participant> _participants = [];

    public string? TripIdText { get; set; }
    public int TripId { get; private set; }

    [ObservableProperty]
    private string _tripName = "";

    [ObservableProperty]
    private bool _isExpensesTab = true;

    [ObservableProperty]
    private bool _isBalancesTab;

    partial void OnIsExpensesTabChanged(bool value) => IsBalancesTab = !value;

    [ObservableProperty]
    private bool _hasExpenses;

    [ObservableProperty]
    private string _totalText = "";

    [ObservableProperty]
    private bool _isSettled;

    [ObservableProperty]
    private bool _hasSuggestions;

    public ObservableCollection<ExpenseRow> Expenses { get; } = [];
    public ObservableCollection<BalanceRow> Balances { get; } = [];
    public ObservableCollection<SettlementRow> Suggestions { get; } = [];

    public TripDetailViewModel(SplittDatabase db) => _db = db;

    /// <summary>Returns false when the trip no longer exists (e.g. deleted from the editor).</summary>
    public async Task<bool> LoadAsync()
    {
        if (!int.TryParse(TripIdText, out var tripId))
            return false;
        TripId = tripId;

        var trip = await _db.GetTripAsync(TripId);
        if (trip is null)
            return false;
        TripName = trip.Name;

        _participants = await _db.GetParticipantsAsync(TripId);
        var expenses = await _db.GetExpensesAsync(TripId);
        var shares = await _db.GetSharesForTripAsync(TripId);
        var names = _participants.ToDictionary(p => p.Id, p => p.Name);

        // --- expense list ---
        Expenses.Clear();
        foreach (var e in expenses)
        {
            var payer = names.GetValueOrDefault(e.PaidById, "؟");
            Expenses.Add(new ExpenseRow(
                e.Id,
                e.IsSettlement ? "تسویه" : (e.Description.Length > 0 ? e.Description : "بدون شرح"),
                e.IsSettlement
                    ? $"{payer} پرداخت کرد · {PersianDate.ToDisplay(e.DateUtc.ToLocalTime())}"
                    : $"پرداخت: {payer} · {PersianDate.ToDisplay(e.DateUtc.ToLocalTime())}",
                MoneyFormat.Format(e.Amount),
                e.IsSettlement));
        }
        HasExpenses = Expenses.Count > 0;
        TotalText = MoneyFormat.FormatToman(expenses.Where(e => !e.IsSettlement).Sum(e => e.Amount));

        // --- balances (always derived, never stored) ---
        var net = BalanceCalculator.ComputeNet(_participants, expenses, shares);
        var maxAbs = net.Values.Select(Math.Abs).DefaultIfEmpty(0).Max();

        Balances.Clear();
        foreach (var p in _participants)
        {
            var value = net[p.Id];
            Balances.Add(new BalanceRow(
                p.Name,
                MoneyFormat.Format(Math.Abs(value)),
                value == 0 ? "تسویه" : value > 0 ? "طلبکار" : "بدهکار",
                IsCreditor: value > 0,
                IsZero: value == 0,
                Fraction: maxAbs == 0 ? 0 : (double)(Math.Abs(value) / maxAbs)));
        }

        // --- settlement suggestions ---
        Suggestions.Clear();
        foreach (var s in SettlementPlanner.Plan(net))
        {
            Suggestions.Add(new SettlementRow(
                s.FromParticipantId,
                s.ToParticipantId,
                $"{names[s.FromParticipantId]} به {names[s.ToParticipantId]}",
                MoneyFormat.FormatToman(s.Amount),
                s.Amount));
        }
        HasSuggestions = Suggestions.Count > 0;
        IsSettled = !HasSuggestions && HasExpenses;
        return true;
    }

    [RelayCommand]
    private Task EditTrip() => Shell.Current.GoToAsync($"trip-editor?tripId={TripId}");

    [RelayCommand]
    private void ShowExpenses() => IsExpensesTab = true;

    [RelayCommand]
    private void ShowBalances() => IsExpensesTab = false;

    [RelayCommand]
    private Task AddExpense() => Shell.Current.GoToAsync($"expense-editor?tripId={TripId}");

    [RelayCommand]
    private async Task OpenExpense(ExpenseRow row)
    {
        if (row.IsSettlement)
        {
            var page = Shell.Current.CurrentPage;
            var delete = await page.DisplayAlertAsync("تسویه", "این تراکنش تسویه حذف شود؟", "حذف", "انصراف");
            if (delete)
            {
                await _db.DeleteExpenseAsync(row.Id);
                await LoadAsync();
            }
            return;
        }

        await Shell.Current.GoToAsync($"expense-editor?tripId={TripId}&expenseId={row.Id}");
    }

    [RelayCommand]
    private async Task RecordSettlement(SettlementRow row)
    {
        var page = Shell.Current.CurrentPage;
        var confirmed = await page.DisplayAlertAsync(
            "ثبت تسویه",
            $"{row.Text} مبلغ {row.AmountText} پرداخت کرد؟",
            "ثبت", "انصراف");
        if (!confirmed)
            return;

        // A settlement is just an expense: debtor pays, creditor holds the whole share.
        var expense = new Expense
        {
            TripId = TripId,
            Description = "تسویه",
            Amount = row.Amount,
            PaidById = row.FromId,
            DateUtc = DateTime.UtcNow,
            IsSettlement = true,
        };
        var share = new ExpenseShare { ParticipantId = row.ToId, Share = row.Amount };
        await _db.SaveExpenseAsync(expense, [share]);
        await LoadAsync();
    }

    [RelayCommand]
    private async Task ExportAsync()
    {
        var trip = await _db.GetTripAsync(TripId);
        if (trip is null)
            return;

        var json = TripExporter.ToJson(
            trip,
            _participants,
            await _db.GetExpensesAsync(TripId),
            await _db.GetSharesForTripAsync(TripId));

        var safeName = string.Join("_", trip.Name.Split(Path.GetInvalidFileNameChars(), StringSplitOptions.RemoveEmptyEntries));
        var path = Path.Combine(FileSystem.CacheDirectory, $"splitt-{safeName}.json");
        await File.WriteAllTextAsync(path, json);

        await Share.Default.RequestAsync(new ShareFileRequest
        {
            Title = $"پشتیبان سفر «{trip.Name}»",
            File = new ShareFile(path),
        });
    }
}
