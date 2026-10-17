using MonoTorrent;
using MonoTorrent.Client;

namespace MediaViewer.Server.Services;

public sealed record MagnetFileInfo(int Index, string Path, long Size);

/// <summary>
/// Fetches a torrent's file list from a magnet link (metadata only — nothing
/// is downloaded beyond the info dictionary).
/// </summary>
public sealed class MagnetInspectorService : IAsyncDisposable
{
    private readonly ClientEngine _engine;
    private readonly string _dir;

    public MagnetInspectorService()
    {
        _dir = Path.Combine(Path.GetTempPath(), "mediaviewer-inspect");
        Directory.CreateDirectory(_dir);
        _engine = new ClientEngine(new EngineSettingsBuilder
        {
            CacheDirectory = Path.Combine(_dir, ".cache"),
            AllowPortForwarding = false,
            AutoSaveLoadMagnetLinkMetadata = true
        }.ToSettings());
    }

    public async Task<List<MagnetFileInfo>> GetFilesAsync(string magnetUri, CancellationToken ct = default)
    {
        var magnet = MagnetLink.Parse(magnetUri);
        var key = magnet.InfoHashes.V1OrV2.ToHex();
        var manager = await _engine.AddAsync(magnet, Path.Combine(_dir, key));
        try
        {
            await manager.StartAsync();
            if (!manager.HasMetadata)
            {
                var wait = manager.WaitForMetadataAsync(ct);
                var limit = Task.Delay(TimeSpan.FromSeconds(60), ct);
                if (await Task.WhenAny(wait, limit) == limit)
                    throw new TimeoutException(
                        "Não consegui obter a lista de arquivos do torrent (sem peers ou trackers?).");
                await wait;
            }

            return manager.Files
                .Select((f, i) => new MagnetFileInfo(i, f.Path, f.Length))
                .ToList();
        }
        finally
        {
            try { await manager.StopAsync(TimeSpan.FromSeconds(5)); } catch { }
            try { await _engine.RemoveAsync(manager, RemoveMode.CacheDataOnly); } catch { }
        }
    }

    public async ValueTask DisposeAsync() => _engine.Dispose();
}
