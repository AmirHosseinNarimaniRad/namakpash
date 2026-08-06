using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Splitt.App.Helpers;
using Splitt.Core.Data;
using Splitt.Core.Models;
using Splitt.Core.Services;

namespace Splitt.App.ViewModels;

public partial class PersonPick : ObservableObject
{
    public int Id { get; init; }
    public string Name { get; init; } = "";

    [ObservableProperty]
    private bool _isSelected;

    [ObservableProperty]
    private string _shareText = "";
}

[QueryProperty(nameof(TripIdText), "tripId")]
[QueryProperty(nameof(ExpenseIdText), "expenseId")]
public partial class ExpenseEditorViewModel : ObservableObject
{
    private readonly SplittDatabase _db;
    private Expense? _expense;
    private bool _loaded;

    public string? TripIdText { get; set; }
    public string? ExpenseIdText { get; set; }

    private int TripId => int.TryParse(TripIdText, out var id) ? id : 0;

    [ObservableProperty]
    private string _pageTitle = "هزینهٔ جدید";

    [ObservableProperty]
    private bool _isEditing;

    [ObservableProperty]
    private string _amountText = "";

    [ObservableProperty]
    private string _description = "";

    [ObservableProperty]
    private bool _isCustomSplit;

    [ObservableProperty]
    private string _splitHint = "";

    [ObservableProperty]
    private bool _hasSplitHint;

    // date (stored Gregorian, shown Jalali)
    [ObservableProperty]
    private string _dateText = "";

    [ObservableProperty]
    private bool _isDatePickerOpen;

    public ObservableCollection<PersonPick> Payers { get; } = [];
    public ObservableCollection<PersonPick> Sharers { get; } = [];

    // Jalali picker state
    public ObservableCollection<int> Years { get; } = [];
    public string[] Months => PersianDate.MonthNames;
    public ObservableCollection<int> Days { get; } = [];

    [ObservableProperty]
    private int _selectedYearIndex;

    [ObservableProperty]
    private int _selectedMonthIndex;

    [ObservableProperty]
    private int _selectedDayIndex;

    private DateTime _dateLocal = DateTime.Now.Date;

    public ExpenseEditorViewModel(SplittDatabase db) => _db = db;

    public async Task LoadAsync()
    {
        if (_loaded || TripId == 0)
            return;
        _loaded = true;

        var participants = await _db.GetParticipantsAsync(TripId);
        var expenses = await _db.GetExpensesAsync(TripId);

        foreach (var p in participants)
        {
            Payers.Add(new PersonPick { Id = p.Id, Name = p.Name });
            Sharers.Add(new PersonPick { Id = p.Id, Name = p.Name, IsSelected = true });
        }

        if (int.TryParse(ExpenseIdText, out var expenseId) && expenseId > 0)
        {
            _expense = expenses.FirstOrDefault(e => e.Id == expenseId);
        }

        if (_expense is not null)
        {
            IsEditing = true;
            PageTitle = "ویرایش هزینه";
            AmountText = MoneyFormat.Format(_expense.Amount);
            Description = _expense.Description;
            _dateLocal = _expense.DateUtc.ToLocalTime().Date;

            var shares = await _db.GetSharesForExpenseAsync(_expense.Id);
            var byId = shares.ToDictionary(s => s.ParticipantId, s => s.Share);
            foreach (var s in Sharers)
            {
                s.IsSelected = byId.ContainsKey(s.Id);
                if (s.IsSelected)
                    s.ShareText = MoneyFormat.Format(byId[s.Id]);
            }
            SetPayer(_expense.PaidById);

            // Detect a custom (unequal) split so editing shows the true state.
            var selected = Sharers.Where(s => s.IsSelected).Select(s => s.Id).ToList();
            if (selected.Count > 0)
            {
                var equal = EqualSplitter.Split(_expense.Amount, selected.Count);
                var actual = selected.Select(id => byId[id]).ToList();
                IsCustomSplit = !equal.SequenceEqual(actual);
            }
        }
        else
        {
            // Fast flow: payer defaults to the most recent expense's payer.
            var lastPayer = expenses
                .Where(e => !e.IsSettlement)
                .OrderByDescending(e => e.Id)
                .Select(e => (int?)e.PaidById)
                .FirstOrDefault() ?? participants.FirstOrDefault()?.Id ?? 0;
            SetPayer(lastPayer);
        }

        DateText = PersianDate.ToLongDisplay(_dateLocal);
        UpdateSplitHint();
    }

    private void SetPayer(int id)
    {
        foreach (var p in Payers)
            p.IsSelected = p.Id == id;
    }

    [RelayCommand]
    private void PickPayer(PersonPick pick) => SetPayer(pick.Id);

    [RelayCommand]
    private void ToggleSharer(PersonPick pick)
    {
        pick.IsSelected = !pick.IsSelected;
        UpdateSplitHint();
    }

    [RelayCommand]
    private void SetEqualSplit()
    {
        IsCustomSplit = false;
        UpdateSplitHint();
    }

    [RelayCommand]
    private void SetCustomSplit()
    {
        IsCustomSplit = true;
        UpdateSplitHint();
    }

    [ObservableProperty]
    private string _amountPreview = "تومان";

    partial void OnAmountTextChanged(string value)
    {
        // Never rewrite Entry.Text while the user is typing (it corrupts the IME's
        // span bookkeeping on Android). Show the separated form in a preview label
        // instead, and prettify the entry itself only on unfocus.
        var parsed = MoneyFormat.Parse(value);
        AmountPreview = parsed is null or 0 ? "تومان" : MoneyFormat.FormatToman(parsed.Value);
        UpdateSplitHint();
    }

    /// <summary>Called when the amount entry loses focus: safe to prettify.</summary>
    public void FormatAmountEntry()
    {
        var parsed = MoneyFormat.Parse(AmountText);
        var formatted = parsed is null or 0 ? "" : MoneyFormat.Format(parsed.Value);
        if (AmountText != formatted)
            AmountText = formatted;
    }

    public void UpdateSplitHint()
    {
        var total = MoneyFormat.Parse(AmountText) ?? 0;
        var selected = Sharers.Where(s => s.IsSelected).ToList();

        if (selected.Count == 0)
        {
            SplitHint = "حداقل یک نفر را انتخاب کن.";
            HasSplitHint = true;
            return;
        }

        if (!IsCustomSplit)
        {
            if (total > 0)
            {
                var shares = EqualSplitter.Split(total, selected.Count);
                SplitHint = shares.Max() == shares.Min()
                    ? $"سهم هر نفر: {MoneyFormat.FormatToman(shares[0])}"
                    : $"سهم هر نفر: {MoneyFormat.Format(shares.Min())} تا {MoneyFormat.Format(shares.Max())} تومان";
                HasSplitHint = true;
            }
            else
            {
                HasSplitHint = false;
                SplitHint = "";
            }
            return;
        }

        var entered = selected.Sum(s => MoneyFormat.Parse(s.ShareText) ?? 0);
        var remainder = total - entered;
        SplitHint = remainder == 0
            ? "سهم‌ها با مبلغ کل برابر است ✓"
            : remainder > 0
                ? $"{MoneyFormat.FormatToman(remainder)} باقی مانده"
                : $"{MoneyFormat.FormatToman(-remainder)} بیشتر از مبلغ کل";
        HasSplitHint = total > 0;
    }

    // ---- Jalali date picker ----

    [RelayCommand]
    private void OpenDatePicker()
    {
        var (y, m, d) = PersianDate.ToJalali(_dateLocal);

        Years.Clear();
        var (currentYear, _, _) = PersianDate.ToJalali(DateTime.Now.Date);
        for (var year = currentYear - 3; year <= currentYear + 1; year++)
            Years.Add(year);

        SelectedYearIndex = Years.IndexOf(y);
        SelectedMonthIndex = m - 1;
        RebuildDays();
        SelectedDayIndex = Math.Min(d - 1, Days.Count - 1);
        IsDatePickerOpen = true;
    }

    partial void OnSelectedYearIndexChanged(int value) => RebuildDays();
    partial void OnSelectedMonthIndexChanged(int value) => RebuildDays();

    private void RebuildDays()
    {
        if (SelectedYearIndex < 0 || SelectedYearIndex >= Years.Count || SelectedMonthIndex < 0)
            return;

        var daysInMonth = PersianDate.DaysInMonth(Years[SelectedYearIndex], SelectedMonthIndex + 1);
        var keep = Math.Min(SelectedDayIndex, daysInMonth - 1);
        Days.Clear();
        for (var d = 1; d <= daysInMonth; d++)
            Days.Add(d);
        SelectedDayIndex = Math.Max(0, keep);
    }

    [RelayCommand]
    private void ConfirmDate()
    {
        if (SelectedYearIndex >= 0 && SelectedMonthIndex >= 0 && SelectedDayIndex >= 0)
        {
            _dateLocal = PersianDate.FromJalali(
                Years[SelectedYearIndex], SelectedMonthIndex + 1, Days[SelectedDayIndex]);
            DateText = PersianDate.ToLongDisplay(_dateLocal);
        }
        IsDatePickerOpen = false;
    }

    [RelayCommand]
    private void CloseDatePicker() => IsDatePickerOpen = false;

    // ---- save / delete ----

    [RelayCommand]
    private async Task SaveAsync()
    {
        var page = Shell.Current.CurrentPage;

        var total = MoneyFormat.Parse(AmountText) ?? 0;
        if (total <= 0)
        {
            await page.DisplayAlertAsync("مبلغ", "مبلغ هزینه را وارد کن.", "باشه");
            return;
        }

        var payer = Payers.FirstOrDefault(p => p.IsSelected);
        if (payer is null)
        {
            await page.DisplayAlertAsync("پرداخت‌کننده", "مشخص کن چه کسی پرداخت کرده است.", "باشه");
            return;
        }

        var selected = Sharers.Where(s => s.IsSelected).ToList();
        if (selected.Count == 0)
        {
            await page.DisplayAlertAsync("تقسیم", "حداقل یک نفر باید در این هزینه سهیم باشد.", "باشه");
            return;
        }

        List<ExpenseShare> shares;
        if (IsCustomSplit)
        {
            var parsed = selected.Select(s => (s.Id, Value: MoneyFormat.Parse(s.ShareText) ?? 0)).ToList();
            if (parsed.Sum(x => x.Value) != total)
            {
                await page.DisplayAlertAsync("تقسیم دستی", "جمع سهم‌ها باید دقیقاً برابر مبلغ کل باشد.", "باشه");
                return;
            }
            shares = parsed
                .Where(x => x.Value > 0)
                .Select(x => new ExpenseShare { ParticipantId = x.Id, Share = x.Value })
                .ToList();
        }
        else
        {
            var equal = EqualSplitter.Split(total, selected.Count);
            shares = selected
                .Select((s, i) => new ExpenseShare { ParticipantId = s.Id, Share = equal[i] })
                .ToList();
        }

        var expense = _expense ?? new Expense { TripId = TripId };
        expense.Description = Description.Trim();
        expense.Amount = total;
        expense.PaidById = payer.Id;
        expense.DateUtc = _dateLocal.ToUniversalTime();

        await _db.SaveExpenseAsync(expense, shares);
        await Shell.Current.GoToAsync("..");
    }

    [RelayCommand]
    private async Task DeleteAsync()
    {
        if (_expense is null)
            return;

        var page = Shell.Current.CurrentPage;
        var confirmed = await page.DisplayAlertAsync("حذف هزینه", "این هزینه برای همیشه حذف شود؟", "حذف", "انصراف");
        if (!confirmed)
            return;

        await _db.DeleteExpenseAsync(_expense.Id);
        await Shell.Current.GoToAsync("..");
    }
}
