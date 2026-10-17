using MediaViewer.App.Services;
using MediaViewer.Streaming;
using Microsoft.Extensions.DependencyInjection;

namespace MediaViewer.App;

/// <summary>
/// Native playback page — MediaElement/ExoPlayer plays MKV (and FLAC audio),
/// which the BlazorWebView's Chromium engine cannot. The page owns the torrent
/// stream handle and disposes it when closed.
/// </summary>
public partial class VideoPlayerPage : ContentPage
{
    private readonly TorrentStreamHandle? _handle;
    private readonly int? _mediaId;
    private readonly int? _profileId;
    private readonly ApiClient? _api;
    private readonly Timer? _progressTimer;
    private bool _closing;

    /// <param name="uri">Playback URL — local torrent http:// URL, a remote direct URL, or a file path.</param>
    /// <param name="handle">Torrent stream to dispose on close; null for direct URLs/files.</param>
    public VideoPlayerPage(string uri, TorrentStreamHandle? handle, int? mediaId)
    {
        InitializeComponent();
        _handle = handle;
        _mediaId = mediaId;

        var services = IPlatformApplication.Current?.Services;
        _api = services?.GetService<ApiClient>();
        _profileId = services?.GetService<AppSettings>()?.ProfileId;

        Player.Source = uri.StartsWith('/')
            ? CommunityToolkit.Maui.Views.MediaSource.FromFile(uri)
            : CommunityToolkit.Maui.Views.MediaSource.FromUri(uri);
        _progressTimer = new Timer(_ => _ = SaveProgressAsync(), null,
            TimeSpan.FromSeconds(10), TimeSpan.FromSeconds(10));
    }

    private async void OnClose(object? sender, EventArgs e) => await CloseAsync();

    protected override bool OnBackButtonPressed()
    {
        _ = CloseAsync();
        return true;
    }

    private async Task CloseAsync()
    {
        if (_closing) return;
        _closing = true;
        _progressTimer?.Dispose();
        await SaveProgressAsync();
        Player.Stop();
        if (_handle is not null)
            await _handle.DisposeAsync();
        await Navigation.PopModalAsync();
    }

    private async Task SaveProgressAsync()
    {
        if (_mediaId is null || _profileId is null || _api is null) return;
        var dur = Player.Duration.TotalSeconds;
        var pos = Player.Position.TotalSeconds;
        if (dur <= 0 || pos <= 0) return;
        try { await _api.UpdateProgressAsync(_profileId.Value, _mediaId.Value, pos, dur); }
        catch { /* offline */ }
    }
}
