using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;

namespace MediaViewer.Streaming;

/// <summary>
/// A single local HTTP server that serves torrent-backed streams to the
/// WebView. MonoTorrent creates one HttpListener per stream, which cannot
/// share a port on Android's managed listener — so we own the listener and
/// multiplex by URL path instead: http://127.0.0.1:{port}/{key}
/// </summary>
internal sealed class TorrentHttpServer : IAsyncDisposable
{
    private readonly int _port;
    private readonly HttpListener _listener;
    private readonly ConcurrentDictionary<string, Stream> _streams = new();
    private readonly ConcurrentDictionary<string, SemaphoreSlim> _locks = new();
    private readonly ConcurrentDictionary<string, string> _contentTypes = new();
    private readonly CancellationTokenSource _cts = new();

    public TorrentHttpServer()
    {
        // Port 5555 is reserved by adbd on emulators; grab a free port instead.
        // If the chosen port is taken between probe and bind, retry.
        for (var attempt = 0; ; attempt++)
        {
            var probe = new TcpListener(IPAddress.Loopback, 0);
            probe.Start();
            _port = ((IPEndPoint)probe.LocalEndpoint).Port;
            probe.Stop();

            _listener = new HttpListener();
            _listener.Prefixes.Add($"http://127.0.0.1:{_port}/");
            try
            {
                _listener.Start();
                break;
            }
            catch when (attempt < 5)
            {
            }
        }
        _ = AcceptLoopAsync();
    }

    public string Register(string key, Stream stream, string contentType = "video/mp4")
    {
        _streams[key] = stream;
        _contentTypes[key] = contentType;
        _locks.TryAdd(key, new SemaphoreSlim(1, 1));
        return $"http://127.0.0.1:{_port}/{key}/";
    }

    public void Unregister(string key)
    {
        if (_streams.TryRemove(key, out var stream))
            stream.Dispose();
        _locks.TryRemove(key, out _);
    }

    private async Task AcceptLoopAsync()
    {
        while (!_cts.IsCancellationRequested)
        {
            try
            {
                var ctx = await _listener.GetContextAsync();
                _ = HandleAsync(ctx);
            }
            catch
            {
                // Listener closed or transient accept failure.
            }
        }
    }

    private async Task HandleAsync(HttpListenerContext ctx)
    {
        var key = ctx.Request.Url?.AbsolutePath.Trim('/') ?? "";
        if (!_streams.TryGetValue(key, out var stream))
        {
            ctx.Response.StatusCode = 404;
            ctx.Response.Close();
            return;
        }

        // LocalStream is seekable, not thread-safe — serialize requests per key.
        var gate = _locks.GetOrAdd(key, _ => new SemaphoreSlim(1, 1));
        await gate.WaitAsync();
        try
        {
            long start = 0, end = stream.Length - 1;
            var range = ctx.Request.Headers["Range"];
            var hasRange = !string.IsNullOrEmpty(range)
                && range.StartsWith("bytes=", StringComparison.OrdinalIgnoreCase);

            if (hasRange)
            {
                var parts = range![6..].Split('-', StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length >= 1 && long.TryParse(parts[0], out var s)) start = s;
                if (parts.Length >= 2 && long.TryParse(parts[1], out var e)) end = e;
                if (start > end || start >= stream.Length)
                {
                    ctx.Response.StatusCode = 416;
                    ctx.Response.Close();
                    return;
                }
                end = Math.Min(end, stream.Length - 1);
            }

            var length = end - start + 1;
            var res = ctx.Response;
            res.StatusCode = hasRange ? 206 : 200;
            res.Headers["Accept-Ranges"] = "bytes";
            res.ContentType = _contentTypes.TryGetValue(key, out var ct2) ? ct2 : "application/octet-stream";
            res.ContentLength64 = length;
            if (hasRange)
                res.Headers["Content-Range"] = $"bytes {start}-{end}/{stream.Length}";

            if (ctx.Request.HttpMethod == "HEAD")
            {
                res.Close();
                return;
            }

            stream.Seek(start, SeekOrigin.Begin);
            var buffer = new byte[64 * 1024];
            var remaining = length;
            while (remaining > 0)
            {
                var read = await stream.ReadAsync(
                    buffer.AsMemory(0, (int)Math.Min(buffer.Length, remaining)), _cts.Token);
                if (read == 0) break;
                await res.OutputStream.WriteAsync(buffer.AsMemory(0, read), _cts.Token);
                remaining -= read;
            }
            res.Close();
        }
        catch
        {
            try { ctx.Response.Abort(); } catch { }
        }
        finally
        {
            gate.Release();
        }
    }

    public async ValueTask DisposeAsync()
    {
        _cts.Cancel();
        foreach (var kv in _streams)
            kv.Value.Dispose();
        _streams.Clear();
        try { _listener.Stop(); } catch { }
        await Task.CompletedTask;
    }
}
