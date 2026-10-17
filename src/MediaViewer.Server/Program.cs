using MediaViewer.Server.Api;
using MediaViewer.Server.Auth;
using MediaViewer.Server.Components;
using MediaViewer.Server.Data;
using MediaViewer.Server.Services;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlite(builder.Configuration.GetConnectionString("Default")
        ?? "Data Source=mediaviewer.db"));

builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.LoginPath = "/admin/login";
        options.ExpireTimeSpan = TimeSpan.FromDays(30);
    })
    .AddScheme<DeviceTokenAuthOptions, DeviceTokenAuthHandler>(
        DeviceTokenAuthHandler.SchemeName, null);

builder.Services.AddAuthorizationBuilder()
    .AddPolicy(DeviceTokenAuthHandler.SchemeName, policy => policy
        .AddAuthenticationSchemes(DeviceTokenAuthHandler.SchemeName)
        .RequireAuthenticatedUser());

builder.Services.AddHttpClient();
builder.Services.AddSingleton<TmdbService>();
builder.Services.AddSingleton<MagnetInspectorService>();
builder.Services.AddSingleton<YouTubeService>();
builder.Services.AddSingleton<YouTubeFeedService>();
builder.Services.AddHostedService(s => s.GetRequiredService<YouTubeFeedService>());

var app = builder.Build();

await using (var scope = app.Services.CreateAsyncScope())
{
    await DbSeeder.InitializeAsync(scope.ServiceProvider);
}

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    app.UseHsts();
}
// Re-executar para /not-found só vale para a UI admin (páginas Razor). Para a API,
// o re-exec reenviava POSTs ao endpoint Razor que exige antiforgery, transformando
// qualquer 4xx com body vazio em um 400 "incorrect Content-type".
app.UseWhen(
    ctx => !ctx.Request.Path.StartsWithSegments("/api"),
    branch => branch.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true));
app.UseHttpsRedirection();

app.UseAuthentication();
app.UseAuthorization();
app.UseAntiforgery();

app.MapStaticAssets();
app.MapApi();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();
