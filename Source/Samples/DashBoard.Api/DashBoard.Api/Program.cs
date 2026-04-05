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

// --- Dashboard API (self-contained: reads connections, interceptors, API key from config) ---
var dashboardSection = builder.Configuration.GetSection("Dashboard");
builder.Services.AddDotNetWorkQueueDashboard(dashboardSection);

// --- Dashboard UI (Blazor Server) ---
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();
builder.Services.AddMudServices();

// --- Authentication ---
var authSection = dashboardSection.GetSection("Auth");
var authUsername = authSection.GetValue<string>("Username") ?? "";
var authPasswordHash = authSection.GetValue<string>("PasswordHash") ?? "";

var authConfig = new DashboardAuthConfig
{
    IsEnabled = !string.IsNullOrEmpty(authUsername),
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

// HttpClient that points back to this same host for API calls
var apiKey = dashboardSection.GetValue<string>("ApiKey") ?? "";
var appUrl = builder.Configuration["ASPNETCORE_URLS"]
             ?? "http://192.168.0.2:9998";
var baseUrl = appUrl.Split(';')[0].Trim();

builder.Services.AddHttpClient<IDashboardApiClient, DashboardApiClient>(client =>
{
    client.BaseAddress = new Uri(baseUrl);
    if (!string.IsNullOrEmpty(apiKey))
        client.DefaultRequestHeaders.Add("X-Api-Key", apiKey);
});

var app = builder.Build();

// --- Middleware ---
app.UseDotNetWorkQueueDashboard();
app.UseAuthentication();
app.MapControllers();

// --- Login / Logout endpoints ---
app.MapPost("/auth/login", async (HttpContext ctx) =>
{
    var form = await ctx.Request.ReadFormAsync();
    var username = form["username"].ToString();
    var password = form["password"].ToString();

    var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(password))).ToLowerInvariant();

    if (string.Equals(username, authConfig.Username, StringComparison.OrdinalIgnoreCase)
        && string.Equals(hash, authConfig.PasswordHash, StringComparison.OrdinalIgnoreCase))
    {
        var claims = new List<Claim> { new(ClaimTypes.Name, username) };
        var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
        await ctx.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, new ClaimsPrincipal(identity));
        ctx.Response.Redirect("/");
    }
    else
    {
        ctx.Response.Redirect("/login?error=1");
    }
});

app.MapGet("/auth/logout", async (HttpContext ctx) =>
{
    await ctx.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
    ctx.Response.Redirect("/login");
});

// --- UI middleware ---
app.UseStaticFiles();
app.UseAntiforgery();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

Log.Information("Dashboard API + UI starting...");
Log.Information("Swagger: {Url}/swagger", baseUrl);
Log.Information("Dashboard UI: {Url}", baseUrl);
app.Run();
