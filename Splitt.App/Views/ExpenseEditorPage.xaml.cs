using Splitt.App.ViewModels;

namespace Splitt.App.Views;

public partial class ExpenseEditorPage : ContentPage
{
    private readonly ExpenseEditorViewModel _vm;

    public ExpenseEditorPage(ExpenseEditorViewModel vm)
    {
        InitializeComponent();
        BindingContext = _vm = vm;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await _vm.LoadAsync();

        // Fast flow: focus the amount first for a new expense.
        if (!_vm.IsEditing)
        {
            await Task.Delay(200);
            AmountEntry.Focus();
        }
    }

    private void OnShareTextChanged(object? sender, TextChangedEventArgs e) =>
        _vm.UpdateSplitHint();

    private void OnAmountUnfocused(object? sender, FocusEventArgs e) =>
        _vm.FormatAmountEntry();
}
