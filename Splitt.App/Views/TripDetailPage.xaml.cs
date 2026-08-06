using Splitt.App.ViewModels;

namespace Splitt.App.Views;

public partial class TripDetailPage : ContentPage
{
    private readonly TripDetailViewModel _vm;

    public TripDetailPage(TripDetailViewModel vm)
    {
        InitializeComponent();
        BindingContext = _vm = vm;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        var exists = await _vm.LoadAsync();
        if (!exists)
            await Shell.Current.GoToAsync("..");
    }
}
