using Microsoft.Extensions.Logging;
using Splitt.App.ViewModels;
using Splitt.App.Views;
using Splitt.Core.Data;

namespace Splitt.App;

public static class MauiProgram
{
	public static MauiApp CreateMauiApp()
	{
		var builder = MauiApp.CreateBuilder();
		builder
			.UseMauiApp<App>()
			.ConfigureFonts(fonts =>
			{
				fonts.AddFont("Vazirmatn-Regular.ttf", "Vazirmatn");
				fonts.AddFont("Vazirmatn-Medium.ttf", "VazirmatnMedium");
				fonts.AddFont("Vazirmatn-SemiBold.ttf", "VazirmatnSemiBold");
				fonts.AddFont("Vazirmatn-Bold.ttf", "VazirmatnBold");
			});

		var dbPath = Path.Combine(FileSystem.AppDataDirectory, "splitt.db3");
		builder.Services.AddSingleton(new SplittDatabase(dbPath));

		builder.Services.AddTransient<TripsViewModel>();
		builder.Services.AddTransient<TripEditorViewModel>();
		builder.Services.AddTransient<TripDetailViewModel>();
		builder.Services.AddTransient<ExpenseEditorViewModel>();

		builder.Services.AddTransient<TripsPage>();
		builder.Services.AddTransient<TripEditorPage>();
		builder.Services.AddTransient<TripDetailPage>();
		builder.Services.AddTransient<ExpenseEditorPage>();

#if DEBUG
		builder.Logging.AddDebug();
#endif

		return builder.Build();
	}
}
