using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Splitt.Core.Data;
using Splitt.Core.Export;
using Splitt.Core.Helpers;
using Splitt.Core.Models;
using Splitt.Core.Services;

namespace Splitt.App.ViewModels;

public sealed record ExpenseRow(
    int Id, string Description, string Meta, string SharesText, bool HasShares, string AmountText, bool IsSettlement);

public sealed record BalanceRow(string Name, string AmountText, string StatusText, bool IsCreditor, bool IsZero, double Fraction);

public sealed record SettlementRow(int FromId, int ToId, string Text, string AmountText, decimal Amount);

public sealed record ReportItemRow(string Description, string DateText, string AmountText);

/// <summary>One person's card on the report tab. Mutable only in its expanded flag.</summary>
public sealed partial class PersonReportRow : ObservableObject
{
    public required string Name { get; init; }
    public required string SummaryText { get; init; }
    public required string NetAmountText { get; init; }
    public required string NetLabel { get; init; }
    public required bool IsCreditor { get; init; }
    public required bool IsZero { get; init; }
    public required IReadOnlyList<ReportItemRow> PaidItems { get; init; }
    public required IReadOnlyList<ReportItemRow> ShareItems { get; init; }
    public required string SettledText { get; init; }

    public bool HasPaidItems => PaidItems.Count > 0;
    public bool HasShareItems => ShareItems.Count > 0;
    public bool HasSettled => SettledText.Length > 0;

    [ObservableProperty]
    private bool _isExpanded;
}

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

    [ObservableProperty]
    private bool _isReportTab;

    [ObservableProperty]
    private bool _hasExpenses;

    [ObservableProperty]
    private string _totalText = "";

    [ObservableProperty]
    private string _averageText = "";

    [ObservableProperty]
    private bool _isSettled;

    [ObservableProperty]
    private bool _hasSuggestions;

    public ObservableCollection<ExpenseRow> Expenses { get; } = [];
    public ObservableCollection<BalanceRow> Balances { get; } = [];
    public ObservableCollection<SettlementRow> Suggestions { get; } = [];
    public ObservableCollection<PersonReportRow> ReportRows { get; } = [];

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
        var order = _participants.Select((p, i) => (p.Id, i)).ToDictionary(x => x.Id, x => x.i);
        var sharesByExpense = shares.ToLookup(s => s.ExpenseId);

        // --- expense list ---
        Expenses.Clear();
        foreach (var e in expenses)
        {
            var payer = names.GetValueOrDefault(e.PaidById, "؟");
            var date = PersianDate.ToDisplayWithTime(e.DateUtc.ToLocalTime());

            string meta, sharesText;
            if (e.IsSettlement)
            {
                var recipient = sharesByExpense[e.Id]
                    .Select(s => names.GetValueOrDefault(s.ParticipantId, "؟"))
                    .FirstOrDefault() ?? "؟";
                // RLM: with a Latin payer name the line would flip to LTR and read reversed.
                meta = Bidi.Rtl($"{payer} به {recipient} · {date}");
                sharesText = "";
            }
            else
            {
                meta = $"پرداخت: {payer} · {date}";
                sharesText = "سهم‌ها: " + string.Join(" · ", sharesByExpense[e.Id]
                    .OrderBy(s => order.GetValueOrDefault(s.ParticipantId, int.MaxValue))
                    .Select(s => $"{names.GetValueOrDefault(s.ParticipantId, "؟")} {MoneyFormat.Format(s.Share)}"));
            }

            Expenses.Add(new ExpenseRow(
                e.Id,
                e.IsSettlement ? "تسویه" : (e.Description.Length > 0 ? e.Description : "بدون شرح"),
                meta,
                sharesText,
                HasShares: sharesText.Length > 0,
                MoneyFormat.Format(e.Amount),
                e.IsSettlement));
        }
        HasExpenses = Expenses.Count > 0;

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
                // RLM: "Sara به Amir" must not render as "Amir به Sara" (see Bidi).
                Bidi.Rtl($"{names[s.FromParticipantId]} به {names[s.ToParticipantId]}"),
                MoneyFormat.FormatToman(s.Amount),
                s.Amount));
        }
        HasSuggestions = Suggestions.Count > 0;
        IsSettled = !HasSuggestions && HasExpenses;

        // --- report (derived like everything else) ---
        var report = ReportBuilder.Build(_participants, expenses, shares);
        TotalText = MoneyFormat.FormatToman(report.Total);
        AverageText = MoneyFormat.FormatToman(report.AveragePerPerson);

        var expanded = ReportRows.Where(r => r.IsExpanded).Select(r => r.Name).ToHashSet();
        ReportRows.Clear();
        foreach (var p in report.People)
        {
            var settledParts = new List<string>();
            if (p.SettledPaid > 0)
                settledParts.Add($"تسویهٔ پرداختی: {MoneyFormat.Format(p.SettledPaid)}");
            if (p.SettledReceived > 0)
                settledParts.Add($"تسویهٔ دریافتی: {MoneyFormat.Format(p.SettledReceived)}");

            ReportRows.Add(new PersonReportRow
            {
                Name = p.Name,
                SummaryText = $"پرداخت: {MoneyFormat.Format(p.Paid)} · سهم: {MoneyFormat.Format(p.Owed)}",
                NetAmountText = MoneyFormat.Format(Math.Abs(p.Net)),
                NetLabel = p.Net == 0 ? "تسویه" : p.Net > 0 ? "طلبکار" : "بدهکار",
                IsCreditor = p.Net > 0,
                IsZero = p.Net == 0,
                PaidItems = ToItemRows(p.PaidItems),
                ShareItems = ToItemRows(p.ShareItems),
                SettledText = string.Join(" · ", settledParts),
                IsExpanded = expanded.Contains(p.Name),
            });
        }

        return true;
    }

    private static List<ReportItemRow> ToItemRows(IReadOnlyList<ReportItem> items) =>
        items.Select(i => new ReportItemRow(
            i.Description.Length > 0 ? i.Description : "بدون شرح",
            PersianDate.ToDisplay(i.DateUtc.ToLocalTime()),
            MoneyFormat.Format(i.Amount)))
        .ToList();

    private void SetTab(bool expenses, bool balances, bool report)
    {
        IsExpensesTab = expenses;
        IsBalancesTab = balances;
        IsReportTab = report;
    }

    [RelayCommand]
    private Task EditTrip() => Shell.Current.GoToAsync($"trip-editor?tripId={TripId}");

    [RelayCommand]
    private void ShowExpenses() => SetTab(true, false, false);

    [RelayCommand]
    private void ShowBalances() => SetTab(false, true, false);

    [RelayCommand]
    private void ShowReport() => SetTab(false, false, true);

    [RelayCommand]
    private void ToggleReportRow(PersonReportRow row) => row.IsExpanded = !row.IsExpanded;

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
    private async Task ShareReportAsync()
    {
        var trip = await _db.GetTripAsync(TripId);
        if (trip is null)
            return;

        var text = ReportTextFormatter.Format(
            trip.Name,
            _participants,
            await _db.GetExpensesAsync(TripId),
            await _db.GetSharesForTripAsync(TripId));

        await Share.Default.RequestAsync(new ShareTextRequest
        {
            Title = $"گزارش سفر «{trip.Name}»",
            Text = text,
        });
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
