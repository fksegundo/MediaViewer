using System.Text.Json;
using System.Text.Json.Serialization;

namespace MediaViewer.Server.Services;

public sealed record TmdbSearchResult(
    int TmdbId,
    string Title,
    string? Overview,
    string? PosterUrl,
    string? BackdropUrl,
    int? Year,
    string MediaType);

public sealed record TmdbEpisode(
    int EpisodeNumber,
    string Name,
    string? Overview,
    string? StillUrl);

public sealed class TmdbService(IHttpClientFactory httpFactory, IConfiguration config)
{
    private const string ImageBase = "https://image.tmdb.org/t/p/";
    private string? ApiKey => config["Tmdb:ApiKey"];

    public bool IsConfigured => !string.IsNullOrWhiteSpace(ApiKey);

    public static string? ToImageUrl(string? path, string size = "w500") =>
        string.IsNullOrEmpty(path) ? null
        : path.StartsWith("http", StringComparison.OrdinalIgnoreCase) ? path
        : $"{ImageBase}{size}{path}";

    public async Task<List<TmdbSearchResult>> SearchAsync(string query, CancellationToken ct = default)
    {
        if (!IsConfigured || string.IsNullOrWhiteSpace(query))
            return [];

        var http = httpFactory.CreateClient();
        var url = $"https://api.themoviedb.org/3/search/multi?api_key={ApiKey}&query={Uri.EscapeDataString(query)}&language=pt-BR&include_adult=false";
        var doc = await http.GetFromJsonAsync<JsonElement>(url, ct);

        var results = new List<TmdbSearchResult>();
        foreach (var r in doc.GetProperty("results").EnumerateArray())
        {
            var mediaType = r.TryGetProperty("media_type", out var mt) ? mt.GetString() : null;
            if (mediaType is not ("movie" or "tv"))
                continue;

            var title = mediaType == "movie"
                ? r.GetProperty("title").GetString()
                : r.GetProperty("name").GetString();
            var dateProp = mediaType == "movie" ? "release_date" : "first_air_date";
            int? year = null;
            if (r.TryGetProperty(dateProp, out var d) && d.GetString() is { Length: >= 4 } ds)
                year = int.TryParse(ds[..4], out var y) ? y : null;

            results.Add(new TmdbSearchResult(
                r.GetProperty("id").GetInt32(),
                title ?? "?",
                r.TryGetProperty("overview", out var ov) ? ov.GetString() : null,
                ToImageUrl(r.TryGetProperty("poster_path", out var pp) ? pp.GetString() : null),
                ToImageUrl(r.TryGetProperty("backdrop_path", out var bp) ? bp.GetString() : null, "w1280"),
                year,
                mediaType));
        }
        return results;
    }

    /// <summary>Lists the episodes of a TV season (title, overview, still).</summary>
    public async Task<List<TmdbEpisode>> GetSeasonEpisodesAsync(
        int tmdbId, int season, CancellationToken ct = default)
    {
        if (!IsConfigured)
            return [];

        var http = httpFactory.CreateClient();
        var url = $"https://api.themoviedb.org/3/tv/{tmdbId}/season/{season}?api_key={ApiKey}&language=pt-BR";
        var doc = await http.GetFromJsonAsync<JsonElement>(url, ct);

        var episodes = new List<TmdbEpisode>();
        foreach (var e in doc.GetProperty("episodes").EnumerateArray())
        {
            episodes.Add(new TmdbEpisode(
                e.GetProperty("episode_number").GetInt32(),
                e.GetProperty("name").GetString() ?? "",
                e.TryGetProperty("overview", out var ov) ? ov.GetString() : null,
                ToImageUrl(e.TryGetProperty("still_path", out var sp) ? sp.GetString() : null, "w300")));
        }
        return episodes;
    }
}
