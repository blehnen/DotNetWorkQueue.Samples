using DotNetWorkQueue.Dashboard.Api;
using DotNetWorkQueue.Dashboard.Api.Configuration;
using DotNetWorkQueue.Transport.LiteDb.Basic;
using DotNetWorkQueue.Transport.PostgreSQL.Basic;
using DotNetWorkQueue.Transport.Redis.Basic;
using DotNetWorkQueue.Transport.SqlServer.Basic;
using DotNetWorkQueue.Transport.SQLite.Basic;
using Serilog;

Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Information()
    .WriteTo.Console()
    .CreateLogger();

var builder = WebApplication.CreateBuilder(args);
builder.Host.UseSerilog();

var dashboardConfig = builder.Configuration.GetSection("Dashboard");

builder.Services.AddDotNetWorkQueueDashboard(options =>
{
    options.EnableSwagger = dashboardConfig.GetValue("EnableSwagger", true);

    foreach (var conn in dashboardConfig.GetSection("Connections").GetChildren())
    {
        var transport = conn["Transport"];
        var connectionString = conn["ConnectionString"];
        var displayName = conn["DisplayName"] ?? transport;
        var queues = conn.GetSection("Queues").Get<string[]>() ?? Array.Empty<string>();

        AddConnectionByTransport(options, transport!, connectionString!, displayName, queues);
    }
});

var app = builder.Build();
app.UseDotNetWorkQueueDashboard();
app.MapControllers();

Log.Information("Dashboard API starting...");
app.Run();

static void AddConnectionByTransport(DashboardOptions options, string transport,
    string connectionString, string displayName, string[] queues)
{
    switch (transport)
    {
        case "SqlServer":
            options.AddConnection<SqlServerMessageQueueInit>(connectionString, conn =>
            {
                conn.DisplayName = displayName;
                foreach (var queue in queues)
                    conn.AddQueue(queue);
            });
            break;
        case "PostgreSql":
            options.AddConnection<PostgreSqlMessageQueueInit>(connectionString, conn =>
            {
                conn.DisplayName = displayName;
                foreach (var queue in queues)
                    conn.AddQueue(queue);
            });
            break;
        case "SQLite":
            options.AddConnection<SqLiteMessageQueueInit>(connectionString, conn =>
            {
                conn.DisplayName = displayName;
                foreach (var queue in queues)
                    conn.AddQueue(queue);
            });
            break;
        case "LiteDb":
            options.AddConnection<LiteDbMessageQueueInit>(connectionString, conn =>
            {
                conn.DisplayName = displayName;
                foreach (var queue in queues)
                    conn.AddQueue(queue);
            });
            break;
        case "Redis":
            options.AddConnection<RedisQueueInit>(connectionString, conn =>
            {
                conn.DisplayName = displayName;
                foreach (var queue in queues)
                    conn.AddQueue(queue);
            });
            break;
        default:
            throw new ArgumentException($"Unknown transport type: {transport}");
    }
}
