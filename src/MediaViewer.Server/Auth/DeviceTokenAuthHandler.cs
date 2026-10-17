using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Encodings.Web;
using MediaViewer.Server.Data;
using Microsoft.AspNetCore.Authentication;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace MediaViewer.Server.Auth;

public sealed class DeviceTokenAuthOptions : AuthenticationSchemeOptions;

/// <summary>
/// Opaque bearer token auth for the mobile app. Tokens are random 256-bit strings
/// stored as SHA-256 hashes in DeviceTokens, so they can be revoked in the admin panel.
/// </summary>
public sealed class DeviceTokenAuthHandler(
    IOptionsMonitor<DeviceTokenAuthOptions> options,
    ILoggerFactory logger,
    UrlEncoder encoder,
    IServiceScopeFactory scopeFactory)
    : AuthenticationHandler<DeviceTokenAuthOptions>(options, logger, encoder)
{
    public const string SchemeName = "Device";

    public static string HashToken(string token) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token)));

    protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        var header = Request.Headers.Authorization.ToString();
        if (!header.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
            return AuthenticateResult.NoResult();

        var token = header["Bearer ".Length..].Trim();
        if (token.Length < 16)
            return AuthenticateResult.Fail("Token inválido");

        await using var scope = scopeFactory.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var hash = HashToken(token);

        var device = await db.DeviceTokens
            .FirstOrDefaultAsync(d => d.TokenHash == hash && !d.Revoked);

        if (device is null)
            return AuthenticateResult.Fail("Token revogado ou inexistente");

        var identity = new ClaimsIdentity(SchemeName);
        identity.AddClaim(new Claim("deviceId", device.Id.ToString()));
        identity.AddClaim(new Claim("deviceName", device.DeviceName));
        return AuthenticateResult.Success(
            new AuthenticationTicket(new ClaimsPrincipal(identity), SchemeName));
    }
}
