namespace MediaViewer.Shared;

public enum MediaKind
{
    Movie = 0,
    Series = 1,
    Episode = 2
}

// Extensible for future sources (S3, GoogleDrive, LocalFile).
public enum SourceKind
{
    Torrent = 0,
    Direct = 1
}

public sealed record ProfileDto(int Id, string Name, string AvatarColor);

public sealed record SourceDto(
    int Id,
    SourceKind Kind,
    string Uri,
    int? FileIndex,
    string? FileName,
    long? FileSize);

public sealed record MediaItemDto(
    int Id,
    string Title,
    string? Overview,
    string? PosterUrl,
    string? BackdropUrl,
    int? Year,
    MediaKind Kind,
    int? ParentId,
    string? ContentRating,
    List<SourceDto> Sources,
    List<MediaItemDto> Episodes);

public sealed record CatalogDto(
    List<MediaItemDto> ContinueWatching,
    List<MediaItemDto> Movies,
    List<MediaItemDto> Series);

public sealed record YouTubeVideoDto(
    string VideoId,
    string Title,
    string? ThumbnailUrl,
    string ChannelName,
    DateTime PublishedAt);

public sealed record ProgressUpdateDto(
    int ProfileId,
    int MediaItemId,
    double PositionSeconds,
    double DurationSeconds);

public sealed record PairRequestDto(string Code, string DeviceName);

public sealed record PairResponseDto(string Token);
