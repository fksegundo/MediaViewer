using System.Security.Claims;
using System.Security.Cryptography;
using MediaViewer.Server.Auth;
using MediaViewer.Server.Data;
using MediaViewer.Server.Services;
using MediaViewer.Shared;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace MediaViewer.Server.Api;

public static class ApiEndpoints
{
    public static void MapApi(this WebApplication app)
    {
        // ---------- Admin auth (cookie, form posts) ----------

        app.MapPost("/admin/signin", async (HttpContext ctx, AppDbContext db) =>
        {
            var form = await ctx.Request.ReadFormAsync();
            var username = form["username"].ToString();
            var password = form["password"].ToString();

            var user = await db.ParentUsers.FirstOrDefaultAsync(u => u.Username == username);
            var hasher = new PasswordHasher<ParentUser>();
            if (user is null || hasher.VerifyHashedPassword(user, user.PasswordHash, password)
                    is PasswordVerificationResult.Failed)
                return Results.Redirect("/admin/login?error=1");

            var identity = new ClaimsIdentity(CookieAuthenticationDefaults.AuthenticationScheme);
            identity.AddClaim(new Claim(ClaimTypes.Name, user.Username));
            await ctx.SignInAsync(
                CookieAuthenticationDefaults.AuthenticationScheme,
                new ClaimsPrincipal(identity));
            return Results.Redirect("/admin");
        }).AllowAnonymous().DisableAntiforgery();

        app.MapPost("/admin/signout", async (HttpContext ctx) =>
        {
            await ctx.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            return Results.Redirect("/admin/login");
        }).DisableAntiforgery();

        // ---------- Device pairing ----------

        app.MapPost("/api/pair", async (HttpRequest http, AppDbContext db) =>
        {
            var req = await http.ReadFromJsonAsync<PairRequestDto>();
            if (req is null)
                return Results.BadRequest();

            var code = await db.PairingCodes
                .FirstOrDefaultAsync(p => p.Code == req.Code && !p.Consumed);

            if (code is null || code.ExpiresAt < DateTime.UtcNow)
                return Results.Unauthorized();

            code.Consumed = true;
            var token = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));
            db.DeviceTokens.Add(new DeviceToken
            {
                DeviceName = string.IsNullOrWhiteSpace(req.DeviceName) ? "Dispositivo" : req.DeviceName,
                TokenHash = DeviceTokenAuthHandler.HashToken(token),
                CreatedAt = DateTime.UtcNow
            });
            await db.SaveChangesAsync();
            return Results.Ok(new PairResponseDto(token));
        }).AllowAnonymous().DisableAntiforgery();

        // ---------- Device API (bearer token) ----------

        var api = app.MapGroup("/api")
            .RequireAuthorization(DeviceTokenAuthHandler.SchemeName);

        api.MapGet("/profiles", async (AppDbContext db) =>
            await db.Profiles
                .Select(p => new ProfileDto(p.Id, p.Name, p.AvatarColor))
                .ToListAsync());

        api.MapGet("/catalog", async (int? profileId, AppDbContext db) =>
        {
            var items = await db.MediaItems
                .Include(m => m.Episodes.OrderBy(e => e.SortOrder))
                .ThenInclude(e => e.Sources)
                .Include(m => m.Sources)
                .Where(m => m.ParentId == null)
                .OrderBy(m => m.SortOrder)
                .ToListAsync();

            var continueWatching = new List<MediaItemDto>();
            if (profileId is not null)
            {
                var inProgress = await db.WatchProgresses
                    .Where(w => w.ProfileId == profileId && w.PositionSeconds > 10
                            && (w.DurationSeconds <= 0 || w.PositionSeconds < w.DurationSeconds * 0.95))
                    .OrderByDescending(w => w.UpdatedAt)
                    .ToListAsync();

                var byId = items.SelectMany(m => m.Episodes.Append(m))
                    .ToDictionary(m => m.Id);
                foreach (var w in inProgress)
                    if (byId.TryGetValue(w.MediaItemId, out var item))
                        continueWatching.Add(ToDto(item));
            }

            return new CatalogDto(
                continueWatching,
                items.Where(m => m.Kind == MediaKind.Movie).Select(ToDto).ToList(),
                items.Where(m => m.Kind == MediaKind.Series).Select(ToDto).ToList());
        });

        api.MapGet("/media/{id:int}", async (int id, AppDbContext db) =>
        {
            var item = await db.MediaItems
                .Include(m => m.Sources)
                .Include(m => m.Episodes.OrderBy(e => e.SortOrder))
                .ThenInclude(e => e.Sources)
                .FirstOrDefaultAsync(m => m.Id == id);
            return item is null ? Results.NotFound() : Results.Ok(ToDto(item));
        });

        api.MapGet("/youtube/feed", async (AppDbContext db) =>
            await db.YouTubeVideos
                .Where(v => v.IsShort != true)
                .OrderByDescending(v => v.PublishedAt)
                .Take(200)
                .Select(v => new YouTubeVideoDto(
                    v.VideoId, v.Title, v.ThumbnailUrl, v.Channel.Name, v.PublishedAt))
                .ToListAsync());

        api.MapPost("/progress", async (HttpRequest http, AppDbContext db) =>
        {
            var req = await http.ReadFromJsonAsync<ProgressUpdateDto>();
            if (req is null)
                return Results.BadRequest();

            var progress = await db.WatchProgresses
                .FirstOrDefaultAsync(w => w.ProfileId == req.ProfileId && w.MediaItemId == req.MediaItemId);
            if (progress is null)
            {
                progress = new WatchProgress
                {
                    ProfileId = req.ProfileId,
                    MediaItemId = req.MediaItemId
                };
                db.WatchProgresses.Add(progress);
            }
            progress.PositionSeconds = req.PositionSeconds;
            progress.DurationSeconds = req.DurationSeconds;
            progress.UpdatedAt = DateTime.UtcNow;
            await db.SaveChangesAsync();
            return Results.Ok();
        });
    }

    private static MediaItemDto ToDto(MediaItem m) => new(
        m.Id, m.Title, m.Overview,
        TmdbService.ToImageUrl(m.PosterPath),
        TmdbService.ToImageUrl(m.BackdropPath, "w1280"),
        m.Year, m.Kind, m.ParentId, m.ContentRating,
        m.Sources.OrderBy(s => s.OrderHint)
            .Select(s => new SourceDto(s.Id, s.Kind, s.Uri, s.FileIndex, s.FileName, s.FileSize))
            .ToList(),
        m.Episodes.OrderBy(e => e.SortOrder).Select(ToDto).ToList());
}
