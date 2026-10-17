namespace MediaViewer.App;

public partial class App : Application
{
	public App()
	{
		InitializeComponent();
	}

	protected override Window CreateWindow(IActivationState? activationState)
	{
		var back = IPlatformApplication.Current?.Services.GetService<Services.AppBackStack>();
		return new Window(back is null ? new MainPage() : new MainPage(back)) { Title = "MediaViewer.App" };
	}
}
