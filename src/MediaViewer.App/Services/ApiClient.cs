using System.Net.Http.Json;
using MediaViewer.Shared;

namespace MediaViewer.App.Services;

/// <summary>Typed client for the MediaViewer server API.</summary>
public sealed class ApiClient
{
    private readonly AppSettings _settings;
    private readonly HttpClient _http;

    public ApiClient(AppSettings settings)
    {
        _settings = settings;
        _http = new HttpClient { Timeout = TimeSpan.FromSeconds(30) };
    }

    private HttpRequestMessage CreateRequest(HttpMethod method, string path)
    {
        if (_settings.ServerUrl is null)
            throw new InvalidOperationException("Servidor não configurado.");
        var req = new HttpRequestMessage(method, $"{_settings.ServerUrl}{path}");
        if (_settings.DeviceToken is not null)
            req.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", _settings.DeviceToken);
        return req;
    }

    private async Task<T?> GetAsync<T>(string path, CancellationToken ct = default)
    {
        using var req = CreateRequest(HttpMethod.Get, path);
        using var resp = await _http.SendAsync(req, ct);
        resp.EnsureSuccessStatusCode();
        return await resp.Content.ReadFromJsonAsync<T>(ct);
    }

    public async Task<string?> PairAsync(string code, string deviceName, CancellationToken ct = default)
    {
        using var req = CreateRequest(HttpMethod.Post, "/api/pair");
        req.Content = JsonContent.Create(new PairRequestDto(code, deviceName));
        using var resp = await _http.SendAsync(req, ct);
        if (!resp.IsSuccessStatusCode)
            return null;
        var result = await resp.Content.ReadFromJsonAsync<PairResponseDto>(ct);
        return result?.Token;
    }

    public Task<List<ProfileDto>?> GetProfilesAsync(CancellationToken ct = default)
        => GetAsync<List<ProfileDto>>("/api/profiles", ct);

    public Task<CatalogDto?> GetCatalogAsync(int? profileId = null, CancellationToken ct = default)
        => GetAsync<CatalogDto>(profileId is null ? "/api/catalog" : $"/api/catalog?profileId={profileId}", ct);

    public Task<MediaItemDto?> GetMediaAsync(int id, CancellationToken ct = default)
        => GetAsync<MediaItemDto>($"/api/media/{id}", ct);

    public Task<List<YouTubeVideoDto>?> GetYouTubeFeedAsync(CancellationToken ct = default)
        => GetAsync<List<YouTubeVideoDto>>("/api/youtube/feed", ct);

    public async Task UpdateProgressAsync(int profileId, int mediaItemId, double position, double duration,
        CancellationToken ct = default)
    {
        using var req = CreateRequest(HttpMethod.Post, "/api/progress");
        req.Content = JsonContent.Create(new ProgressUpdateDto(profileId, mediaItemId, position, duration));
        using var resp = await _http.SendAsync(req, ct);
        resp.EnsureSuccessStatusCode();
    }
}
