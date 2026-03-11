using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using DotNetWorkQueue.Dashboard.Api;
using DotNetWorkQueue.Dashboard.Api.Configuration;
using DotNetWorkQueue.Dashboard.Ui.Services;
using DotNetWorkQueue.Transport.LiteDb.Basic;
using DotNetWorkQueue.Transport.PostgreSQL.Basic;
using DotNetWorkQueue.Transport.Redis.Basic;
using DotNetWorkQueue.Transport.SqlServer.Basic;
using DotNetWorkQueue.Transport.SQLite.Basic;
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

var dashboardConfig = builder.Configuration.GetSection("Dashboard");

// Bind interceptor options from JSON (shared across all queues in this sample)
var interceptorOptions = dashboardConfig.GetSection("Interceptors").Get<DashboardInterceptorOptions>();

// --- Dashboard API ---
builder.Services.AddDotNetWorkQueueDashboard(options =>
{
    options.EnableSwagger = dashboardConfig.GetValue("EnableSwagger", true);
    options.ApiKey = dashboardConfig.GetValue<string>("ApiKey") ?? string.Empty;

    foreach (var conn in dashboardConfig.GetSection("Connections").GetChildren())
    {
        var transport = conn["Transport"];
        var connectionString = conn["ConnectionString"];
        var displayName = conn["DisplayName"] ?? transport;
        var queues = conn.GetSection("Queues").Get<string[]>() ?? Array.Empty<string>();

        AddConnectionByTransport(options, transport!, connectionString!, displayName, queues, interceptorOptions);
    }
});

// --- Dashboard UI (Blazor Server) ---
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();
builder.Services.AddMudServices();

// --- Dashboard UI Auth ---
var authSection = dashboardConfig.GetSection("Auth");
var authUsername = authSection.GetValue<string>("Username") ?? string.Empty;
var authPasswordHash = authSection.GetValue<string>("PasswordHash") ?? string.Empty;

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
var apiKey = dashboardConfig.GetValue<string>("ApiKey") ?? string.Empty;
var appUrl = builder.Configuration["ASPNETCORE_URLS"]
             ?? "https://localhost:32906";
var baseUrl = appUrl.Split(';')[0].Trim();

builder.Services.AddHttpClient<IDashboardApiClient, DashboardApiClient>(client =>
{
    client.BaseAddress = new Uri(baseUrl);
    if (!string.IsNullOrEmpty(apiKey))
        client.DefaultRequestHeaders.Add("X-Api-Key", apiKey);
});

var app = builder.Build();

// --- API middleware ---
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
app.MapRazorComponents<DotNetWorkQueue.Dashboard.Ui.Components.App>()
    .AddInteractiveServerRenderMode();

Log.Information("Dashboard API + UI starting...");
Log.Information("Swagger: {Url}/swagger", baseUrl);
Log.Information("Dashboard UI: {Url}", baseUrl);
app.Run();

static void AddConnectionByTransport(DashboardOptions options, string transport,
    string connectionString, string displayName, string[] queues,
    DashboardInterceptorOptions interceptors)
{
    switch (transport)
    {
        case "SqlServer":
            options.AddConnection<SqlServerMessageQueueInit>(connectionString, conn =>
            {
                conn.DisplayName = displayName;
                foreach (var queue in queues)
                    conn.AddQueue(queue, interceptors);
            });
            break;
        case "PostgreSql":
            options.AddConnection<PostgreSqlMessageQueueInit>(connectionString, conn =>
            {
                conn.DisplayName = displayName;
                foreach (var queue in queues)
                    conn.AddQueue(queue, interceptors);
            });
            break;
        case "SQLite":
            options.AddConnection<SqLiteMessageQueueInit>(connectionString, conn =>
            {
                conn.DisplayName = displayName;
                foreach (var queue in queues)
                    conn.AddQueue(queue, interceptors);
            });
            break;
        case "LiteDb":
            options.AddConnection<LiteDbMessageQueueInit>(connectionString, conn =>
            {
                conn.DisplayName = displayName;
                foreach (var queue in queues)
                    conn.AddQueue(queue, interceptors);
            });
            break;
        case "Redis":
            options.AddConnection<RedisQueueInit>(connectionString, conn =>
            {
                conn.DisplayName = displayName;
                foreach (var queue in queues)
                    conn.AddQueue(queue, interceptors);
            });
            break;
        default:
            throw new ArgumentException($"Unknown transport type: {transport}");
    }
}
