using Splitt.App.Views;

namespace Splitt.App;

public partial class AppShell : Shell
{
	public AppShell()
	{
		InitializeComponent();

		Routing.RegisterRoute("trip-editor", typeof(TripEditorPage));
		Routing.RegisterRoute("trip-detail", typeof(TripDetailPage));
		Routing.RegisterRoute("expense-editor", typeof(ExpenseEditorPage));
	}
}
