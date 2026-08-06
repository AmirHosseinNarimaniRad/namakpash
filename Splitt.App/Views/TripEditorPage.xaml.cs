using Splitt.App.ViewModels;

namespace Splitt.App.Views;

public partial class TripEditorPage : ContentPage
{
    private readonly TripEditorViewModel _vm;

    public TripEditorPage(TripEditorViewModel vm)
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
