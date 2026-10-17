using CommunityToolkit.Maui;
using MediaViewer.App.Services;
using Microsoft.Extensions.Logging;

namespace MediaViewer.App;

public static class MauiProgram
{
	public static MauiApp CreateMauiApp()
	{
		var builder = MauiApp.CreateBuilder();
		builder
			.UseMauiApp<App>()
			.ConfigureFonts(fonts =>
			{
				fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
			})
			.UseMauiCommunityToolkit()
			.UseMauiCommunityToolkitMediaElement(true);

		builder.Services.AddMauiBlazorWebView();

		builder.Services.AddSingleton<AppSettings>();
		builder.Services.AddSingleton<AppBackStack>();
		builder.Services.AddSingleton<ApiClient>();
		builder.Services.AddSingleton<TorrentService>();
		builder.Services.AddSingleton<DownloadStore>();

#if DEBUG
		builder.Services.AddBlazorWebViewDeveloperTools();
		builder.Logging.AddDebug();
#endif

		return builder.Build();
	}
}
