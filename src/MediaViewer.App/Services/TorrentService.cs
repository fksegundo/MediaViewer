using MediaViewer.Streaming;
using MonoTorrent.Client;

namespace MediaViewer.App.Services;

/// <summary>
/// Wraps the MonoTorrent engine. Files are stored under the app data dir
/// so they survive restarts and can be played offline.
/// </summary>
public sealed class TorrentService : IAsyncDisposable
{
    private readonly TorrentEngine _engine;

    public TorrentService()
    {
        var dir = Path.Combine(FileSystem.AppDataDirectory, "torrents");
        _engine = new TorrentEngine(dir);
    }

    /// <summary>Resolves a magnet into a local http:// playback URL.</summary>
    public Task<TorrentStreamHandle> GetStreamAsync(string magnetUri, int? fileIndex = null,
        CancellationToken ct = default)
        => _engine.GetStreamAsync(magnetUri, fileIndex, ct);

    /// <summary>Starts a background download of the full torrent.</summary>
    public Task<TorrentManager> StartDownloadAsync(string magnetUri, CancellationToken ct = default)
        => _engine.StartDownloadAsync(magnetUri, ct);

    /// <summary>Resolves when the chosen file is fully on disk.</summary>
    public Task<MonoTorrent.ITorrentManagerFile> WaitForFileCompleteAsync(
        TorrentManager manager, int? fileIndex, CancellationToken ct = default)
        => _engine.WaitForFileCompleteAsync(manager, fileIndex, ct);

    public TorrentManager? GetManager(string magnetUri) => _engine.GetManager(magnetUri);

    public Task RemoveAsync(TorrentManager manager, bool deleteData, CancellationToken ct = default)
        => _engine.RemoveAsync(manager, deleteData, ct);

    public string DownloadDirectory => Path.Combine(FileSystem.AppDataDirectory, "torrents");

    public ValueTask DisposeAsync() => _engine.DisposeAsync();
}
