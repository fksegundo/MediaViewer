using System.Text.Json;
using MediaViewer.Server.Data;
using Microsoft.EntityFrameworkCore;

namespace MediaViewer.Server.Services;

public sealed record ResolvedChannel(string ChannelId, string Name, string? ThumbnailUrl, string UploadsPlaylistId);

public sealed class YouTubeService(IHttpClientFactory httpFactory, IConfiguration config)
{
    private string? ApiKey => config["YouTube:ApiKey"];
    public bool IsConfigured => !string.IsNullOrWhiteSpace(ApiKey);

    /// <summary>
    /// Accepts a channel URL (@handle, /channel/UC..., /c/...) or a bare channel id
    /// and resolves it via the YouTube Data API.
    /// </summary>
    public async Task<ResolvedChannel?> ResolveChannelAsync(string input, CancellationToken ct = default)
    {
        if (!IsConfigured || string.IsNullOrWhiteSpace(input))
            return null;

        input = input.Trim();
        var http = httpFactory.CreateClient();

        // Extract a handle or id from common URL shapes.
        string? handle = null, channelId = null;
        if (input.StartsWith("UC") && input.Length >= 20 && !input.Contains('/'))
            channelId = input;
        else if (Uri.TryCreate(input, UriKind.Absolute, out var uri))
        {
            var path = uri.AbsolutePath.Trim('/');
            if (path.StartsWith("@")) handle = path.Split('/')[0];
            else if (path.StartsWith("channel/")) channelId = path.Split('/')[1];
            else if (path.StartsWith("c/") || path.StartsWith("user/")) handle = "@" + path.Split('/')[1];
        }
        else if (input.StartsWith('@'))
            handle = input.Split('/')[0];

        string url;
        if (channelId is not null)
            url = $"https://www.googleapis.com/youtube/v3/channels?part=snippet,contentDetails&id={channelId}&key={ApiKey}";
        else if (handle is not null)
            url = $"https://www.googleapis.com/youtube/v3/channels?part=snippet,contentDetails&forHandle={Uri.EscapeDataString(handle)}&key={ApiKey}";
        else
            return null;

        var doc = await http.GetFromJsonAsync<JsonElement>(url, ct);
        var items = doc.GetProperty("items");
        if (items.GetArrayLength() == 0)
            return null;

        var item = items[0];
        var snippet = item.GetProperty("snippet");
        return new ResolvedChannel(
            item.GetProperty("id").GetString()!,
            snippet.GetProperty("title").GetString()!,
            snippet.GetProperty("thumbnails").GetProperty("default").GetProperty("url").GetString(),
            item.GetProperty("contentDetails").GetProperty("relatedPlaylists").GetProperty("uploads").GetString()!);
    }

    /// <summary>Fetches the latest uploads for a channel (up to 50).</summary>
    public async Task<List<(string VideoId, string Title, string? Thumb, DateTime Published)>> FetchUploadsAsync(
        string uploadsPlaylistId, CancellationToken ct = default)
    {
        var http = httpFactory.CreateClient();
        var url = $"https://www.googleapis.com/youtube/v3/playlistItems?part=snippet,contentDetails&playlistId={uploadsPlaylistId}&maxResults=50&key={ApiKey}";
        var doc = await http.GetFromJsonAsync<JsonElement>(url, ct);

        var videos = new List<(string, string, string?, DateTime)>();
        foreach (var item in doc.GetProperty("items").EnumerateArray())
        {
            var snippet = item.GetProperty("snippet");
            var videoId = snippet.GetProperty("resourceId").GetProperty("videoId").GetString();
            if (videoId is null) continue;

            string? thumb = null;
            if (snippet.TryGetProperty("thumbnails", out var thumbs)
                && thumbs.TryGetProperty("medium", out var medium))
                thumb = medium.GetProperty("url").GetString();

            var published = snippet.TryGetProperty("publishedAt", out var pa)
                && DateTime.TryParse(pa.GetString(), out var dt)
                    ? dt : DateTime.MinValue;

            videos.Add((videoId, snippet.GetProperty("title").GetString() ?? "", thumb, published));
        }
        return videos;
    }

    // Reused so we can observe redirects without following them.
    private static readonly HttpClient NoRedirectHttp =
        new(new HttpClientHandler { AllowAutoRedirect = false });

    /// <summary>
    /// Detects YouTube Shorts: youtube.com/shorts/{id} renders (200) for Shorts
    /// and 303-redirects to /watch for regular videos.
    /// </summary>
    public async Task<bool> IsShortAsync(string videoId, CancellationToken ct = default)
    {
        try
        {
            using var res = await NoRedirectHttp.GetAsync(
                $"https://www.youtube.com/shorts/{videoId}", ct);
            return res.StatusCode == System.Net.HttpStatusCode.OK;
        }
        catch
        {
            return false;
        }
    }
}

public sealed class YouTubeFeedService(
    IServiceScopeFactory scopeFactory,
    YouTubeService yt,
    IConfiguration config,
    ILogger<YouTubeFeedService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!yt.IsConfigured)
        {
            logger.LogWarning("YouTube:ApiKey não configurada — feed desativado.");
            return;
        }

        var interval = TimeSpan.FromHours(config.GetValue("YouTube:RefreshHours", 6));
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await RefreshAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Falha ao atualizar feed do YouTube");
            }
            await Task.Delay(interval, stoppingToken);
        }
    }

    public async Task RefreshAsync(CancellationToken ct = default)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var channels = await db.YouTubeChannels.Where(c => c.UploadsPlaylistId != null).ToListAsync(ct);
        foreach (var channel in channels)
        {
            var uploads = await yt.FetchUploadsAsync(channel.UploadsPlaylistId!, ct);
            foreach (var (videoId, title, thumb, published) in uploads)
            {
                var exists = await db.YouTubeVideos.AnyAsync(v => v.VideoId == videoId, ct);
                if (exists) continue;

                db.YouTubeVideos.Add(new YouTubeVideo
                {
                    VideoId = videoId,
                    ChannelId = channel.Id,
                    Title = title,
                    ThumbnailUrl = thumb,
                    PublishedAt = published,
                    IsShort = await yt.IsShortAsync(videoId, ct)
                });
            }
            await db.SaveChangesAsync(ct);
        }

        // Backfill: probe vídeos antigos que ainda não foram verificados.
        var uncheckedVideos = await db.YouTubeVideos
            .Where(v => v.IsShort == null)
            .Take(100)
            .ToListAsync(ct);
        foreach (var video in uncheckedVideos)
            video.IsShort = await yt.IsShortAsync(video.VideoId, ct);
        if (uncheckedVideos.Count > 0)
            await db.SaveChangesAsync(ct);
    }
}
