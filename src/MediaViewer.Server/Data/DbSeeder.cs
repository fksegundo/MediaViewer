using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace MediaViewer.Server.Data;

public static class DbSeeder
{
    /// <summary>
    /// Applies pending migrations and seeds the initial parent user from config
    /// (Admin:Username / Admin:Password) when no user exists.
    /// </summary>
    public static async Task InitializeAsync(IServiceProvider services)
    {
        var db = services.GetRequiredService<AppDbContext>();
        await db.Database.MigrateAsync();

        if (await db.ParentUsers.AnyAsync())
            return;

        var config = services.GetRequiredService<IConfiguration>();
        var username = config["Admin:Username"] ?? "admin";
        var password = config["Admin:Password"] ?? "admin";

        var hasher = new PasswordHasher<ParentUser>();
        var user = new ParentUser { Username = username, PasswordHash = "" };
        user.PasswordHash = hasher.HashPassword(user, password);
        db.ParentUsers.Add(user);
        await db.SaveChangesAsync();
    }
}
