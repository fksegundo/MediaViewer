using System.Text.Json;

namespace MediaViewer.App.Services;

/// <summary>
/// Tracks completed offline downloads in a JSON file in the app data dir.
/// Each entry maps a MediaItem/Source to a local file path.
/// </summary>
public sealed class DownloadStore
{
    public sealed record Entry(
        int MediaItemId,
        int SourceId,
        string Title,
        string? PosterUrl,
        string LocalPath,
        long FileSize,
        DateTime DownloadedAt,
        string MagnetUri,
        int? FileIndex);

    private readonly string _storePath;
    private List<Entry> _entries = [];
    private readonly SemaphoreSlim _lock = new(1, 1);
    private bool _loaded;

    public DownloadStore()
    {
        _storePath = Path.Combine(FileSystem.AppDataDirectory, "downloads.json");
    }

    private async Task EnsureLoadedAsync()
    {
        if (_loaded) return;
        await _lock.WaitAsync();
        try
        {
            if (_loaded) return;
            if (File.Exists(_storePath))
            {
                var json = await File.ReadAllTextAsync(_storePath);
                _entries = JsonSerializer.Deserialize<List<Entry>>(json) ?? [];
            }
            _loaded = true;
        }
        finally { _lock.Release(); }
    }

    private async Task SaveAsync()
    {
        var json = JsonSerializer.Serialize(_entries);
        await File.WriteAllTextAsync(_storePath, json);
    }

    public async Task<IReadOnlyList<Entry>> GetAllAsync()
    {
        await EnsureLoadedAsync();
        return _entries.Where(e => File.Exists(e.LocalPath)).ToList();
    }

    public async Task<Entry?> FindAsync(int sourceId)
    {
        await EnsureLoadedAsync();
        return _entries.FirstOrDefault(e => e.SourceId == sourceId && File.Exists(e.LocalPath));
    }

    public async Task AddAsync(Entry entry)
    {
        await EnsureLoadedAsync();
        await _lock.WaitAsync();
        try
        {
            _entries.RemoveAll(e => e.SourceId == entry.SourceId);
            _entries.Add(entry);
            await SaveAsync();
        }
        finally { _lock.Release(); }
    }

    /// <summary>Downloads a direct http(s) media URL to local storage. Returns the file path.</summary>
    public static async Task<string> DownloadHttpAsync(string url, CancellationToken ct = default)
    {
        var dir = Path.Combine(FileSystem.AppDataDirectory, "files");
        Directory.CreateDirectory(dir);
        var name = Uri.TryCreate(url, UriKind.Absolute, out var u)
            ? Path.GetFileName(u.LocalPath) : null;
        var path = Path.Combine(dir, $"{Guid.NewGuid():N}_{(string.IsNullOrEmpty(name) ? "video.mp4" : name)}");
        using var http = new HttpClient { Timeout = TimeSpan.FromHours(3) };
        using var resp = await http.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, ct);
        resp.EnsureSuccessStatusCode();
        await using var src = await resp.Content.ReadAsStreamAsync(ct);
        await using var dst = File.Create(path);
        await src.CopyToAsync(dst, ct);
        return path;
    }

    public async Task RemoveAsync(int sourceId, bool deleteFile = true)
    {
        await EnsureLoadedAsync();
        await _lock.WaitAsync();
        try
        {
            var entry = _entries.FirstOrDefault(e => e.SourceId == sourceId);
            if (entry is not null)
            {
                if (deleteFile && File.Exists(entry.LocalPath))
                    File.Delete(entry.LocalPath);
                _entries.Remove(entry);
                await SaveAsync();
            }
        }
        finally { _lock.Release(); }
    }
}
