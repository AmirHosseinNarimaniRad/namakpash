using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Splitt.Core.Helpers;
using Splitt.Core.Data;
using Splitt.Core.Export;

namespace Splitt.App.ViewModels;

public sealed record TripCard(int Id, string Name, string SubText, string TotalText);

public partial class TripsViewModel : ObservableObject
{
    private readonly SplittDatabase _db;
    private int _loadGeneration;

    public ObservableCollection<TripCard> Trips { get; } = [];

    [ObservableProperty]
    private bool _isEmpty;

    [ObservableProperty]
    private bool _isBusy;

    public TripsViewModel(SplittDatabase db) => _db = db;

    public async Task LoadAsync()
    {
        await _db.InitializeAsync();

        // OnAppearing is async void, so two loads can overlap - popping back from a deleted
        // trip navigates twice in a row and reveals this page while a load is still running.
        // Build the whole list first and swap it in without awaiting, or the two runs
        // interleave around the Clear() and cards get added twice.
        var generation = ++_loadGeneration;

        var cards = new List<TripCard>();
        foreach (var trip in await _db.GetTripsAsync())
        {
            var people = await _db.GetParticipantsAsync(trip.Id);
            var expenses = await _db.GetExpensesAsync(trip.Id);
            var total = expenses.Where(e => !e.IsSettlement).Sum(e => e.Amount);

            cards.Add(new TripCard(
                trip.Id,
                trip.Name,
                $"{people.Count} نفر · {expenses.Count(e => !e.IsSettlement)} هزینه",
                MoneyFormat.FormatToman(total)));
        }

        if (generation != _loadGeneration)
            return;

        Trips.Clear();
        foreach (var card in cards)
            Trips.Add(card);

        IsEmpty = Trips.Count == 0;
    }

    [RelayCommand]
    private Task AddTrip() => Shell.Current.GoToAsync("trip-editor");

    [RelayCommand]
    private Task OpenTrip(TripCard card) => Shell.Current.GoToAsync($"trip-detail?tripId={card.Id}");

    [RelayCommand]
    private async Task ImportAsync()
    {
        try
        {
            var file = await FilePicker.Default.PickAsync(new PickOptions
            {
                PickerTitle = "انتخاب فایل پشتیبان",
            });
            if (file is null)
                return;

            IsBusy = true;
            var json = await File.ReadAllTextAsync(file.FullPath);
            var dto = TripExporter.FromJson(json);
            var trip = await _db.ImportTripAsync(dto);
            await LoadAsync();

            if (Shell.Current?.CurrentPage is Page page)
                await page.DisplayAlertAsync("بازیابی شد", $"سفر «{trip.Name}» با موفقیت وارد شد.", "باشه");
        }
        catch (Exception)
        {
            if (Shell.Current?.CurrentPage is Page page)
                await page.DisplayAlertAsync("خطا", "فایل انتخاب‌شده یک پشتیبان معتبر «نمک‌پاش» نیست.", "باشه");
        }
        finally
        {
            IsBusy = false;
        }
    }
}
