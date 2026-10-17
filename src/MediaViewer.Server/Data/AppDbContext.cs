using Microsoft.EntityFrameworkCore;

namespace MediaViewer.Server.Data;

public sealed class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<Profile> Profiles => Set<Profile>();
    public DbSet<MediaItem> MediaItems => Set<MediaItem>();
    public DbSet<MediaSource> MediaSources => Set<MediaSource>();
    public DbSet<YouTubeChannel> YouTubeChannels => Set<YouTubeChannel>();
    public DbSet<YouTubeVideo> YouTubeVideos => Set<YouTubeVideo>();
    public DbSet<ParentUser> ParentUsers => Set<ParentUser>();
    public DbSet<DeviceToken> DeviceTokens => Set<DeviceToken>();
    public DbSet<PairingCode> PairingCodes => Set<PairingCode>();
    public DbSet<WatchProgress> WatchProgresses => Set<WatchProgress>();

    protected override void OnModelCreating(ModelBuilder mb)
    {
        mb.Entity<MediaItem>()
            .HasOne(m => m.Parent)
            .WithMany(m => m.Episodes)
            .HasForeignKey(m => m.ParentId)
            .OnDelete(DeleteBehavior.Cascade);

        mb.Entity<MediaSource>()
            .HasOne(s => s.MediaItem)
            .WithMany(m => m.Sources)
            .HasForeignKey(s => s.MediaItemId)
            .OnDelete(DeleteBehavior.Cascade);

        mb.Entity<YouTubeVideo>()
            .HasOne(v => v.Channel)
            .WithMany(c => c.Videos)
            .HasForeignKey(v => v.ChannelId)
            .OnDelete(DeleteBehavior.Cascade);

        mb.Entity<WatchProgress>()
            .HasIndex(w => new { w.ProfileId, w.MediaItemId })
            .IsUnique();

        mb.Entity<YouTubeVideo>().HasIndex(v => v.VideoId).IsUnique();
        mb.Entity<YouTubeChannel>().HasIndex(c => c.ChannelId).IsUnique();
        mb.Entity<ParentUser>().HasIndex(u => u.Username).IsUnique();
        mb.Entity<PairingCode>().HasIndex(p => p.Code);
    }
}
