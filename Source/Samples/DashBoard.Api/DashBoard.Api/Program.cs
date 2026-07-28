using System.Linq;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using DotNetWorkQueue.Dashboard.Api;
using DotNetWorkQueue.Dashboard.Ui.Components;
using DotNetWorkQueue.Dashboard.Ui.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using MudBlazor.Services;
using Serilog;

Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Information()
    .WriteTo.Console()
    .CreateLogger();

var builder = WebApplication.CreateBuilder(args);
builder.Host.UseSerilog();

// --- Razor / Blazor / MudBlazor services ---
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();
builder.Services.AddMudServices();

// --- Self-contained mode: embed Dashboard API in this process ---
var dashboardSection = builder.Configuration.GetSection("Dashboard");
var selfContained = dashboardSection.GetSection("Connections").GetChildren().Any();
if (selfContained)
{
    builder.Services.AddDotNetWorkQueueDashboard(dashboardSection);
}

// --- Multi-source Dashboard API client registration (0.9.31+) ---
DashboardConfigParser.ValidateNoLegacyConfig(builder.Configuration);
var sources = ResolveSources(builder, selfContained);

var registry = new SourceRegistry(sources);
builder.Services.AddSingleton<ISourceRegistry>(registry);

RegisterSourceHttpClients(builder, sources);

builder.Services.AddSingleton<IMultiSourceDashboardApiClient, MultiSourceDashboardApiClient>();

// --- Health monitoring ---
builder.Services.AddSingleton<ISourceHealthMonitor, SourceHealthMonitor>();
builder.Services.AddHostedService(sp => (SourceHealthMonitor)sp.GetRequiredService<ISourceHealthMonitor>());

// --- Authentication ---
var authUsername = builder.Configuration["DashboardAuth:Username"] ?? "";
var authPasswordHash = builder.Configuration["DashboardAuth:PasswordHash"] ?? "";
var authEnabled = authUsername.Length > 0 && authPasswordHash.Length > 0;

var authConfig = new DashboardAuthConfig
{
    IsEnabled = authEnabled,
    Username = authUsername,
    PasswordHash = authPasswordHash
};
builder.Services.AddSingleton(authConfig);

builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.LoginPath = "/login";
        options.ExpireTimeSpan = TimeSpan.FromHours(8);
    });

if (!string.IsNullOrEmpty(authUsername) && string.IsNullOrEmpty(authPasswordHash))
{
    Log.Warning("DashboardAuth:Username is set but DashboardAuth:PasswordHash is empty. "
        + "Authentication is DISABLED until a password hash is configured. "
        + "Generate a SHA256 hash via: echo -n '<your password>' | sha256sum | cut -d' ' -f1");
}

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
}

if (selfContained)
{
    app.UseDotNetWorkQueueDashboard();
}

app.UseRouting();
app.UseAuthentication();
app.UseAntiforgery();
app.UseStaticFiles();

if (selfContained)
{
    app.MapControllers();
}

// --- Login / Logout endpoints (sample-specific cookie auth) ---
MapAuthEndpoints(app, authConfig);

app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

Log.Information("Dashboard API + UI starting...");
await app.RunAsync();

// --- Startup helpers ---------------------------------------------------------------
// Pulled out of the top-level statements so each step reads as one decision. Local
// functions keep them scoped to startup rather than becoming part of a public surface.

// Resolves the configured API sources, falling back to a local one so the UI always has
// something to point at.
static List<DashboardApiSourceConfig> ResolveSources(WebApplicationBuilder builder, bool selfContained)
{
    var sources = DashboardConfigParser.ParseSources(builder.Configuration);

    // In self-contained mode, auto-add a "Local" source if one is not already configured
    if (selfContained && sources.All(s => !string.Equals(s.Name, "Local", StringComparison.OrdinalIgnoreCase)))
    {
        var localName = builder.Configuration["DashboardApi:LocalSourceName"] ?? "Local";
        sources.Add(new DashboardApiSourceConfig { Name = localName, BaseUrl = "http://localhost:5000" });
        builder.Services.AddHostedService<LocalSourceHostedService>();
    }

    // Default fallback: if no sources configured at all, add a default Local source
    if (sources.Count == 0)
    {
        sources.Add(new DashboardApiSourceConfig { Name = "Local", BaseUrl = "http://localhost:5000" });
    }

    return sources;
}

// One named HttpClient per source, carrying that source's API key if it has one.
static void RegisterSourceHttpClients(WebApplicationBuilder builder, List<DashboardApiSourceConfig> sources)
{
    foreach (var source in sources)
    {
        builder.Services.AddHttpClient(source.Slug, client =>
        {
            client.BaseAddress = new Uri(source.BaseUrl);
            if (!string.IsNullOrEmpty(source.ApiKey))
                client.DefaultRequestHeaders.Add("X-Api-Key", source.ApiKey);
        });
    }
}

static void MapAuthEndpoints(WebApplication app, DashboardAuthConfig authConfig)
{
    app.MapPost("/auth/login", async (HttpContext ctx) =>
    {
        var form = await ctx.Request.ReadFormAsync().ConfigureAwait(false);
        var username = form["username"].ToString();
        var password = form["password"].ToString();

        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(password))).ToLowerInvariant();

        if (string.Equals(username, authConfig.Username, StringComparison.OrdinalIgnoreCase)
            && string.Equals(hash, authConfig.PasswordHash, StringComparison.OrdinalIgnoreCase))
        {
            var claims = new List<Claim> { new(ClaimTypes.Name, username) };
            var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
            await ctx.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, new ClaimsPrincipal(identity)).ConfigureAwait(false);
            ctx.Response.Redirect("/");
        }
        else
        {
            ctx.Response.Redirect("/login?error=1");
        }
    });

    app.MapGet("/auth/logout", async (HttpContext ctx) =>
    {
        await ctx.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme).ConfigureAwait(false);
        ctx.Response.Redirect("/login");
    });
}
