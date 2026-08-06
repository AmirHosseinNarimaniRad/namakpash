using Splitt.App.ViewModels;

namespace Splitt.App.Views;

public partial class TripsPage : ContentPage
{
    private readonly TripsViewModel _vm;

    public TripsPage(TripsViewModel vm)
    {
        InitializeComponent();
        BindingContext = _vm = vm;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await _vm.LoadAsync();
    }
}
