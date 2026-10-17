using System.Collections.Concurrent;
using System.Text;
using System.Text.RegularExpressions;
using MonoTorrent;
using MonoTorrent.Client;
using MonoTorrent.Streaming;

namespace MediaViewer.Streaming;

/// <summary>
/// Wrapper around MonoTorrent's ClientEngine that exposes local HTTP URLs
/// for files inside a torrent, suitable for MediaElement playback.
/// MonoTorrent hosts an HttpListener internally (EngineSettings.HttpStreamingPrefix)
/// which already supports HTTP Range requests — seeks are translated into
/// piece prioritisation automatically.
/// </summary>
public sealed class TorrentEngine : IAsyncDisposable
{
    private static readonly string[] VideoExtensions =
        [".mp4", ".mkv", ".avi", ".webm", ".m4v", ".mov", ".mpg", ".mpeg"];

    private static readonly string[] SubtitleExtensions = [".srt", ".vtt"];

    private readonly ClientEngine _engine;
    private readonly string _downloadDir;
    private readonly ConcurrentDictionary<string, TorrentManager> _active = new();
    private readonly ConcurrentDictionary<string, Stream> _streams = new();
    private readonly SemaphoreSlim _gate = new(1, 1);
    private TorrentHttpServer? _http;

    public TorrentEngine(string downloadDirectory)
    {
        _downloadDir = downloadDirectory;
        Directory.CreateDirectory(downloadDirectory);

        // CacheDirectory defaults to Environment.CurrentDirectory/cache, which is
        // '/' (read-only) on Android. Point it somewhere writable so fast-resume
        // and magnet metadata caching work and offline playback can reload
        // metadata without contacting peers.
        var settings = new EngineSettingsBuilder
        {
            CacheDirectory = Path.Combine(downloadDirectory, ".cache"),
            AllowPortForwarding = false,
            AllowLocalPeerDiscovery = false,
            AutoSaveLoadFastResume = true,
            AutoSaveLoadDhtCache = true,
            AutoSaveLoadMagnetLinkMetadata = true,
            UsePartialFiles = true
        };
        _engine = new ClientEngine(settings.ToSettings());
    }

    /// <summary>
    /// Resolves a magnet link into a local http:// URL the player can consume.
    /// Downloads only the pieces needed for playback first (streaming mode).
    /// </summary>
    public async Task<TorrentStreamHandle> GetStreamAsync(
        string magnetUri, int? fileIndex = null, CancellationToken ct = default)
    {
        var magnet = MagnetLink.Parse(magnetUri);
        var key = magnet.InfoHashes.V1OrV2.ToHex();
        var manager = await GetOrAddManagerAsync(magnet, ct);

        await manager.StartAsync();
        if (!manager.HasMetadata)
        {
            // Metadata normally loads instantly from the magnet cache; fetching
            // it from peers should never take more than ~a minute. We race the
            // wait so a stuck manager (e.g. added from a trackerless magnet)
            // times out even if the token is not observed internally.
            var wait = manager.WaitForMetadataAsync(ct);
            var limit = Task.Delay(TimeSpan.FromSeconds(60), ct);
            if (await Task.WhenAny(wait, limit) == limit)
            {
                await RemoveAsync(manager, deleteData: false, ct);
                throw new TimeoutException(
                    "Não encontrei peers para este torrent. Verifique se o magnet tem trackers e seeds ativos.");
            }
            await wait;
        }

        var file = PickFile(manager.Files, fileIndex);
        var streamKey = $"{key}-{manager.Files.IndexOf(file)}";

        // One stream per file — dispose any previous one left behind by a
        // cancelled/abandoned playback.
        if (_streams.TryRemove(streamKey, out var previous))
            previous.Dispose();

        Stream local;
        try
        {
            local = await manager.StreamProvider!.CreateStreamAsync(file, prebuffer: true, ct);
        }
        catch (InvalidOperationException)
        {
            // A failed earlier attempt can leave the manager's private
            // ActiveStream undisposed with no public way to clear it — the
            // only recovery is recreating the manager (metadata reloads from
            // the magnet cache, so this is fast).
            await RemoveAsync(manager, deleteData: false, ct);
            manager = await GetOrAddManagerAsync(magnet, ct);
            await manager.StartAsync();
            if (!manager.HasMetadata)
                await manager.WaitForMetadataAsync(ct);
            file = PickFile(manager.Files, fileIndex);
            streamKey = $"{key}-{manager.Files.IndexOf(file)}";
            local = await manager.StreamProvider!.CreateStreamAsync(file, prebuffer: true, ct);
        }

        // The caller may have navigated away mid-creation; don't leak the stream.
        if (ct.IsCancellationRequested)
        {
            local.Dispose();
            throw new OperationCanceledException(ct);
        }

        _http ??= new TorrentHttpServer();
        var streamKeyCaptured = streamKey;
        _streams[streamKey] = local;
        var playbackUri = _http.Register(streamKey, local, VideoContentType(file.Path));

        var subtitles = await LoadSubtitlesAsync(manager, streamKey, ct);

        return new TorrentStreamHandle(local, file, manager, playbackUri, subtitles,
            () =>
            {
                foreach (var k in _streams.Keys.Where(k => k.StartsWith(streamKeyCaptured)))
                    _http?.Unregister(k);
            });
    }

    /// <summary>
    /// Downloads any .srt/.vtt files in the torrent (small) and exposes them as
    /// WebVTT tracks on the local HTTP server. Best-effort — playback works
    /// without them if the pieces don't arrive quickly.
    /// </summary>
    private async Task<List<SubtitleTrack>> LoadSubtitlesAsync(
        TorrentManager manager, string streamKey, CancellationToken ct)
    {
        var tracks = new List<SubtitleTrack>();
        var subs = manager.Files
            .Where(f => SubtitleExtensions.Contains(Path.GetExtension(f.Path).ToLowerInvariant()))
            .Take(4)
            .ToList();

        foreach (var sub in subs)
        {
            try
            {
                await manager.SetFilePriorityAsync(sub, Priority.Highest);
                var deadline = DateTime.UtcNow.AddSeconds(20);
                while (!sub.BitField.AllTrue)
                {
                    if (DateTime.UtcNow > deadline || ct.IsCancellationRequested)
                        break;
                    await Task.Delay(500, ct);
                }
                if (!sub.BitField.AllTrue)
                    continue;

                var label = Path.GetFileName(sub.Path);
                Stream s = Path.GetExtension(sub.Path).Equals(".srt", StringComparison.OrdinalIgnoreCase)
                    ? SrtToVtt(sub.DownloadCompleteFullPath)
                    : File.OpenRead(sub.DownloadCompleteFullPath);

                var key = $"{streamKey}-sub{tracks.Count}";
                _streams[key] = s;
                tracks.Add(new SubtitleTrack(
                    label, _http!.Register(key, s, "text/vtt"), GuessLanguage(label)));
            }
            catch
            {
                // Subtitles are optional — ignore failures.
            }
        }
        return tracks;
    }

    /// <summary>Converts SRT cue timestamps (comma) to WebVTT (period).</summary>
    private static MemoryStream SrtToVtt(string srtPath)
    {
        var text = File.ReadAllText(srtPath);
        var vtt = "WEBVTT\n\n" + Regex.Replace(text, @"(\d{2}:\d{2}:\d{2}),(\d{3})", "$1.$2");
        return new MemoryStream(Encoding.UTF8.GetBytes(vtt));
    }

    private static string GuessLanguage(string fileName)
    {
        var n = fileName.ToLowerInvariant();
        if (n.Contains("por") || n.Contains("pt")) return "pt-BR";
        if (n.Contains("eng")) return "en";
        if (n.Contains("spa") || n.Contains("esp")) return "es";
        return "und";
    }

    private static string VideoContentType(string path) =>
        Path.GetExtension(path).ToLowerInvariant() switch
        {
            ".webm" => "video/webm",
            ".mkv" => "video/x-matroska",
            ".avi" => "video/x-msvideo",
            _ => "video/mp4"
        };

    /// <summary>
    /// Starts a download of the torrent for offline playback. All managers are
    /// created via AddStreamingAsync so StreamProvider is always available —
    /// this just starts the transfer without creating a stream.
    /// </summary>
    public async Task<TorrentManager> StartDownloadAsync(string magnetUri, CancellationToken ct = default)
    {
        var magnet = MagnetLink.Parse(magnetUri);
        var manager = await GetOrAddManagerAsync(magnet, ct);
        await manager.StartAsync();
        return manager;
    }

    /// <summary>
    /// Resolves after the chosen file is fully downloaded to disk.
    /// </summary>
    public async Task<ITorrentManagerFile> WaitForFileCompleteAsync(
        TorrentManager manager, int? fileIndex, CancellationToken ct = default)
    {
        if (!manager.HasMetadata)
            await manager.WaitForMetadataAsync(ct);

        var file = PickFile(manager.Files, fileIndex);
        await manager.SetFilePriorityAsync(file, Priority.Highest);

        while (!file.BitField.AllTrue)
        {
            ct.ThrowIfCancellationRequested();
            if (manager.State == TorrentState.Error)
                throw new InvalidOperationException("O download do torrent falhou.");
            await Task.Delay(2000, ct);
        }
        return file;
    }

    public TorrentManager? GetManager(string magnetUri)
    {
        if (!MagnetLink.TryParse(magnetUri, out var magnet))
            return null;
        var key = magnet.InfoHashes.V1OrV2.ToHex();
        return _active.TryGetValue(key, out var m) ? m : null;
    }

    /// <summary>Stops and removes a torrent, optionally deleting downloaded data.</summary>
    public async Task RemoveAsync(TorrentManager manager, bool deleteData, CancellationToken ct = default)
    {
        var key = manager.InfoHashes.V1OrV2.ToHex();
        _active.TryRemove(key, out _);
        try
        {
            await manager.StopAsync(TimeSpan.FromSeconds(10));
            await _engine.RemoveAsync(manager,
                deleteData ? RemoveMode.CacheDataAndDownloadedData : RemoveMode.CacheDataOnly);
        }
        catch
        {
            // Best-effort cleanup; engine may already be shutting down.
        }
    }

    private async Task<TorrentManager> GetOrAddManagerAsync(
        MagnetLink magnet, CancellationToken ct)
    {
        var key = magnet.InfoHashes.V1OrV2.ToHex();

        await _gate.WaitAsync(ct);
        try
        {
            if (_active.TryGetValue(key, out var existing))
            {
                if (existing.HasMetadata)
                    return existing;

                // Stuck manager without metadata (e.g. originally added from a
                // magnet with no trackers). Drop it so the current magnet —
                // which may carry different tr= trackers — is used instead.
                _active.TryRemove(key, out _);
                try
                {
                    await existing.StopAsync(TimeSpan.FromSeconds(5));
                    await _engine.RemoveAsync(existing, RemoveMode.CacheDataOnly);
                }
                catch
                {
                    // Best-effort; a dead manager must not block the new one.
                }
            }

            var savePath = Path.Combine(_downloadDir, key);
            var manager = await _engine.AddStreamingAsync(magnet, savePath);
            _active[key] = manager;
            return manager;
        }
        finally
        {
            _gate.Release();
        }
    }

    private static ITorrentManagerFile PickFile(IList<ITorrentManagerFile> files, int? fileIndex)
    {
        if (fileIndex is >= 0 && fileIndex < files.Count)
            return files[fileIndex.Value];

        var videos = files
            .Where(f => VideoExtensions.Contains(Path.GetExtension(f.Path).ToLowerInvariant()))
            .ToList();
        var video = videos
            .Where(f => !f.Path.Contains("sample", StringComparison.OrdinalIgnoreCase)
                     && !f.Path.Contains("trailer", StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(f => f.Length)
            .FirstOrDefault()
            ?? videos.OrderByDescending(f => f.Length).FirstOrDefault();
        return video ?? files.OrderByDescending(f => f.Length).First();
    }

    public async ValueTask DisposeAsync()
    {
        if (_http is not null)
            await _http.DisposeAsync();
        foreach (var manager in _active.Values)
        {
            try { await manager.StopAsync(TimeSpan.FromSeconds(5)); }
            catch { /* shutting down */ }
        }
        _active.Clear();
        _engine.Dispose();
    }
}

/// <summary>A subtitle track served over the local HTTP server.</summary>
public sealed record SubtitleTrack(string Label, string Uri, string Language);

/// <summary>A playable stream over a file inside a torrent.</summary>
public sealed class TorrentStreamHandle : IAsyncDisposable
{
    private readonly Stream _stream;
    private readonly Action? _onDisposed;

    internal TorrentStreamHandle(Stream stream, ITorrentManagerFile file, TorrentManager manager,
        string playbackUri, IReadOnlyList<SubtitleTrack> subtitles, Action? onDisposed = null)
    {
        _stream = stream;
        File = file;
        Manager = manager;
        PlaybackUri = playbackUri;
        Subtitles = subtitles;
        _onDisposed = onDisposed;
    }

    /// <summary>Local URL to feed to the video element.</summary>
    public string PlaybackUri { get; }

    public IReadOnlyList<SubtitleTrack> Subtitles { get; }

    public ITorrentManagerFile File { get; }
    public TorrentManager Manager { get; }

    public ValueTask DisposeAsync()
    {
        _onDisposed?.Invoke();
        _stream.Dispose();
        return ValueTask.CompletedTask;
    }
}
