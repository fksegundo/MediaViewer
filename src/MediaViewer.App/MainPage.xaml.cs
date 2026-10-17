using MediaViewer.App.Services;

namespace MediaViewer.App;

public partial class MainPage : ContentPage
{
	private readonly AppBackStack? _back;

	public MainPage()
	{
		InitializeComponent();
	}

	public MainPage(AppBackStack back) : this()
	{
		_back = back;
	}

	protected override bool OnBackButtonPressed()
	{
		if (_back?.CanGoBack == true)
		{
			_back.GoBack();
			return true;
		}
		return base.OnBackButtonPressed();
	}
}
