using Microsoft.AspNetCore.Components;

namespace MediaViewer.App.Services;

/// <summary>
/// Tracks Blazor navigation so the Android back button can pop pages
/// instead of closing the app.
/// </summary>
public sealed class AppBackStack
{
    private readonly List<string> _history = [];
    private NavigationManager? _nav;

    public void Attach(NavigationManager nav)
    {
        if (_nav is not null) return;
        _nav = nav;
        _history.Add(nav.Uri);
        nav.LocationChanged += (_, e) =>
        {
            var uri = e.Location;
            if (_history.Count >= 2 && Same(_history[^2], uri))
                _history.RemoveAt(_history.Count - 1);
            else if (!Same(_history[^1], uri))
                _history.Add(uri);
        };
    }

    public bool CanGoBack => _history.Count > 1;

    public void GoBack()
    {
        if (CanGoBack)
            _nav?.NavigateTo(_history[^2]);
    }

    private static bool Same(string a, string b)
        => string.Equals(a, b, StringComparison.OrdinalIgnoreCase);
}
