using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Splitt.Core.Data;
using Splitt.Core.Models;

namespace Splitt.App.ViewModels;

public partial class ParticipantItem : ObservableObject
{
    public int Id { get; init; }

    [ObservableProperty]
    private string _name = "";

    public bool CanRemove { get; init; } = true;
}

[QueryProperty(nameof(TripIdText), "tripId")]
public partial class TripEditorViewModel : ObservableObject
{
    private readonly SplittDatabase _db;
    private readonly List<Participant> _removed = [];
    private Trip? _trip;

    public string? TripIdText { get; set; }

    [ObservableProperty]
    private string _name = "";

    [ObservableProperty]
    private string _newParticipantName = "";

    [ObservableProperty]
    private string _pageTitle = "سفر جدید";

    [ObservableProperty]
    private bool _isEditing;

    public ObservableCollection<ParticipantItem> Participants { get; } = [];

    public TripEditorViewModel(SplittDatabase db) => _db = db;

    public async Task LoadAsync()
    {
        if (!int.TryParse(TripIdText, out var tripId) || tripId <= 0 || _trip is not null)
            return;

        _trip = await _db.GetTripAsync(tripId);
        if (_trip is null)
            return;

        IsEditing = true;
        PageTitle = "ویرایش سفر";
        Name = _trip.Name;

        Participants.Clear();
        foreach (var p in await _db.GetParticipantsAsync(tripId))
        {
            var hasActivity = await _db.ParticipantHasActivityAsync(p.Id);
            Participants.Add(new ParticipantItem { Id = p.Id, Name = p.Name, CanRemove = !hasActivity });
        }
    }

    [RelayCommand]
    private void AddParticipant()
    {
        var name = NewParticipantName.Trim();
        if (name.Length == 0)
            return;
        if (Participants.Any(p => p.Name == name))
        {
            NewParticipantName = "";
            return;
        }

        Participants.Add(new ParticipantItem { Id = 0, Name = name });
        NewParticipantName = "";
    }

    [RelayCommand]
    private void RemoveParticipant(ParticipantItem item)
    {
        if (!item.CanRemove)
            return;
        Participants.Remove(item);
        if (item.Id != 0)
            _removed.Add(new Participant { Id = item.Id });
    }

    [RelayCommand]
    private async Task SaveAsync()
    {
        var page = Shell.Current.CurrentPage;
        var name = Name.Trim();

        if (name.Length == 0)
        {
            await page.DisplayAlertAsync("نام سفر", "برای سفر یک نام انتخاب کن.", "باشه");
            return;
        }
        if (Participants.Count < 2)
        {
            await page.DisplayAlertAsync("شرکت‌کننده‌ها", "حداقل دو نفر لازم است تا چیزی برای تقسیم باشد.", "باشه");
            return;
        }

        if (_trip is null)
        {
            await _db.CreateTripAsync(name, Participants.Select(p => p.Name.Trim()));
        }
        else
        {
            _trip.Name = name;
            await _db.UpdateTripAsync(_trip);

            foreach (var removed in _removed)
                await _db.DeleteParticipantAsync(removed.Id);

            foreach (var item in Participants)
            {
                if (item.Id == 0)
                    await _db.AddParticipantAsync(new Participant { TripId = _trip.Id, Name = item.Name.Trim() });
            }
        }

        await Shell.Current.GoToAsync("..");
    }

    [RelayCommand]
    private async Task DeleteTripAsync()
    {
        if (_trip is null)
            return;

        var page = Shell.Current.CurrentPage;
        var confirmed = await page.DisplayAlertAsync(
            "حذف سفر",
            $"سفر «{_trip.Name}» با همهٔ هزینه‌هایش برای همیشه حذف شود؟",
            "حذف", "انصراف");
        if (!confirmed)
            return;

        await _db.DeleteTripAsync(_trip.Id);
        await Shell.Current.GoToAsync("..");
    }
}
