using MediaViewer.Shared;

namespace MediaViewer.Server.Data;

public sealed class Profile
{
    public int Id { get; set; }
    public required string Name { get; set; }
    public string AvatarColor { get; set; } = "#e50914";
}

public sealed class MediaItem
{
    public int Id { get; set; }
    public required string Title { get; set; }
    public string? Overview { get; set; }
    public string? PosterPath { get; set; }
    public string? BackdropPath { get; set; }
    public int? Year { get; set; }
    public MediaKind Kind { get; set; }
    public int? ParentId { get; set; }
    public MediaItem? Parent { get; set; }
    public List<MediaItem> Episodes { get; set; } = [];
    public int? TmdbId { get; set; }
    public string? ContentRating { get; set; }
    public int SortOrder { get; set; }
    public List<MediaSource> Sources { get; set; } = [];
}

public sealed class MediaSource
{
    public int Id { get; set; }
    public int MediaItemId { get; set; }
    public MediaItem MediaItem { get; set; } = null!;
    public SourceKind Kind { get; set; }
    public required string Uri { get; set; }
    public int? FileIndex { get; set; }
    public string? FileName { get; set; }
    public long? FileSize { get; set; }
    public int OrderHint { get; set; }
}

public sealed class YouTubeChannel
{
    public int Id { get; set; }
    public required string ChannelId { get; set; }
    public required string Name { get; set; }
    public string? ThumbnailUrl { get; set; }
    public string? UploadsPlaylistId { get; set; }
    public List<YouTubeVideo> Videos { get; set; } = [];
}

public sealed class YouTubeVideo
{
    public int Id { get; set; }
    public required string VideoId { get; set; }
    public int ChannelId { get; set; }
    public YouTubeChannel Channel { get; set; } = null!;
    public required string Title { get; set; }
    public string? ThumbnailUrl { get; set; }
    public DateTime PublishedAt { get; set; }
    /// <summary>null = ainda não verificado; true/false após o probe /shorts/.</summary>
    public bool? IsShort { get; set; }
}

public sealed class ParentUser
{
    public int Id { get; set; }
    public required string Username { get; set; }
    public required string PasswordHash { get; set; }
}

public sealed class DeviceToken
{
    public int Id { get; set; }
    public required string DeviceName { get; set; }
    public required string TokenHash { get; set; }
    public DateTime CreatedAt { get; set; }
    public bool Revoked { get; set; }
}

public sealed class PairingCode
{
    public int Id { get; set; }
    public required string Code { get; set; }
    public DateTime ExpiresAt { get; set; }
    public bool Consumed { get; set; }
}

public sealed class WatchProgress
{
    public int Id { get; set; }
    public int ProfileId { get; set; }
    public int MediaItemId { get; set; }
    public double PositionSeconds { get; set; }
    public double DurationSeconds { get; set; }
    public DateTime UpdatedAt { get; set; }
}
