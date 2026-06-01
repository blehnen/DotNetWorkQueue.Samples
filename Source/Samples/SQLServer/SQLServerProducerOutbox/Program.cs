using System;
using System.Configuration;
using System.Data.Common;
using System.Linq;
using DotNetWorkQueue;
using DotNetWorkQueue.Configuration;
using DotNetWorkQueue.Transport.RelationalDatabase;
using DotNetWorkQueue.Transport.SqlServer;
using DotNetWorkQueue.Transport.SqlServer.Basic;
using Microsoft.Data.SqlClient;
using SampleShared;
using Serilog;

namespace SQLServerProducerOutbox
{
    class Program
    {
        static void Main(string[] args)
        {
            var log = new LoggerConfiguration()
                .WriteTo.Console(outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] [{SourceContext}] {Message:lj}{NewLine}{Exception}")
                .MinimumLevel.Debug()
                .CreateLogger();
            Log.Logger = log;
            log.Information("Startup");
            log.Information(SharedConfiguration.AllSettings);

            var queueName = ConfigurationManager.AppSettings.ReadSetting("QueueName");
            var connectionString = ConfigurationManager.AppSettings.ReadSetting("Database");
            var queueConnection = new QueueConnection(queueName, connectionString);

            EnsureOrdersTable(connectionString, log);

            using (var createQueueContainer = new QueueCreationContainer<SqlServerMessageQueueInit>(serviceRegister =>
                Injectors.AddInjectors(Helpers.CreateForSerilog(), SharedConfiguration.EnableTrace, SharedConfiguration.EnableMetrics, SharedConfiguration.EnableCompression, SharedConfiguration.EnableEncryption, "SQLServerProducerOutbox", serviceRegister),
                options => Injectors.SetOptions(options, SharedConfiguration.EnableChaos)))
            {
                using (var createQueue =
                    createQueueContainer.GetQueueCreation<SqlServerMessageQueueCreation>(queueConnection))
                {
                    if (!createQueue.QueueExists)
                    {
                        createQueue.Options.EnableDelayedProcessing = true;
                        createQueue.Options.EnableHeartBeat = true;
                        createQueue.Options.EnableMessageExpiration = true;
                        createQueue.Options.EnableStatus = true;
                        createQueue.Options.EnableStatusTable = true;
                        createQueue.Options.EnableHistory = SharedConfiguration.EnableHistory;

                        var result = createQueue.CreateQueue();
                        log.Information(result.Status.ToString());
                    }
                    else
                    {
                        log.Warning("Queue already exists; not creating; note that any setting changes won't be applied");
                    }
                }
            }

            using (var queueContainer = new QueueContainer<SqlServerMessageQueueInit>(serviceRegister =>
                Injectors.AddInjectors(Helpers.CreateForSerilog(), SharedConfiguration.EnableTrace, SharedConfiguration.EnableMetrics, SharedConfiguration.EnableCompression, SharedConfiguration.EnableEncryption, "SQLServerProducerOutbox", serviceRegister),
                options => Injectors.SetOptions(options, SharedConfiguration.EnableChaos)))
            {
                using (var queue = queueContainer.CreateProducer<OrderCreatedEvent>(queueConnection))
                {
                    using (var admin = queueContainer.CreateAdminApi())
                    {
                        admin.AddQueueConnection(queueContainer, queueConnection);
                        RunOutboxLoop(queue, connectionString, admin, log);
                    }
                }
            }

            if (SharedConfiguration.EnableTrace)
                System.Threading.Thread.Sleep(2000);
        }

        private static void EnsureOrdersTable(string connectionString, ILogger log)
        {
            using (var conn = new SqlConnection(connectionString))
            {
                conn.Open();
                using (var cmd = conn.CreateCommand())
                {
                    cmd.CommandText = @"
IF NOT EXISTS (
    SELECT 1 FROM sys.tables t
    JOIN sys.schemas s ON t.schema_id = s.schema_id
    WHERE s.name = N'dbo' AND t.name = N'Orders'
)
BEGIN
    CREATE TABLE [dbo].[Orders] (
        [Id]         INT            IDENTITY(1,1) NOT NULL,
        [OrderId]    UNIQUEIDENTIFIER             NOT NULL,
        [Customer]   NVARCHAR(200)                NOT NULL,
        [Amount]     DECIMAL(18,2)                NOT NULL,
        [CreatedUtc] DATETIME2      DEFAULT SYSUTCDATETIME() NOT NULL,
        CONSTRAINT [PK_Orders] PRIMARY KEY CLUSTERED ([Id])
    );
END";
                    cmd.ExecuteNonQuery();
                }
            }
            log.Information("Orders table ensured");
        }

        private static void RunOutboxLoop(
            IProducerQueue<OrderCreatedEvent> queue,
            string connectionString,
            IAdminApi admin,
            ILogger log)
        {
            var keepRunning = true;
            while (keepRunning)
            {
                var connId = admin.Connections.Keys.FirstOrDefault();
                var waiting = admin.Count(connId, QueueStatusAdmin.Waiting);
                log.Information("Items waiting in queue: {Count}", waiting);

                Console.WriteLine();
                Console.WriteLine("c) Commit   — insert Orders row + send message, then COMMIT");
                Console.WriteLine("r) Rollback — insert + send, then ROLLBACK");
                Console.WriteLine("q) Quit");
                Console.Write("Choice: ");

                var key = char.ToLower(Console.ReadKey(true).KeyChar);
                Console.WriteLine(key);

                switch (key)
                {
                    case 'c':
                        SendOnTransaction(queue, connectionString, commit: true, log);
                        var afterCommit = admin.Count(admin.Connections.Keys.FirstOrDefault(), QueueStatusAdmin.Waiting);
                        log.Information("Queue Waiting count after COMMIT: {Count}", afterCommit);
                        break;
                    case 'r':
                        SendOnTransaction(queue, connectionString, commit: false, log);
                        var afterRollback = admin.Count(admin.Connections.Keys.FirstOrDefault(), QueueStatusAdmin.Waiting);
                        log.Information("Queue Waiting count after ROLLBACK: {Count}", afterRollback);
                        break;
                    case 'q':
                        keepRunning = false;
                        break;
                    default:
                        log.Warning("Unknown key '{Key}' — press c, r, or q", key);
                        break;
                }
            }
        }

        private static void SendOnTransaction(
            IProducerQueue<OrderCreatedEvent> queue,
            string connectionString,
            bool commit,
            ILogger log)
        {
            var order = new OrderCreatedEvent
            {
                OrderId = Guid.NewGuid(),
                Customer = "Sample Customer",
                Amount = 42.50m,
                CreatedUtc = DateTime.UtcNow
            };

            using (var conn = new SqlConnection(connectionString))
            {
                conn.Open();
                using (var tx = conn.BeginTransaction())
                {
                    using (var cmd = conn.CreateCommand())
                    {
                        cmd.Transaction = tx;
                        cmd.CommandText = "INSERT INTO [dbo].[Orders] ([OrderId],[Customer],[Amount],[CreatedUtc]) VALUES (@OrderId,@Customer,@Amount,@CreatedUtc)";
                        cmd.Parameters.AddWithValue("@OrderId", order.OrderId);
                        cmd.Parameters.AddWithValue("@Customer", order.Customer);
                        cmd.Parameters.AddWithValue("@Amount", order.Amount);
                        cmd.Parameters.AddWithValue("@CreatedUtc", order.CreatedUtc);
                        cmd.ExecuteNonQuery();
                    }

                    if (queue is IRelationalProducerQueue<OrderCreatedEvent> relational)
                    {
                        var result = relational.Send(order, tx);
                        log.Information("Send queued: {HasError}", result.HasError);
                    }
                    else
                    {
                        throw new InvalidOperationException("Producer does not support IRelationalProducerQueue (outbox requires SqlServer/PostgreSQL transport).");
                    }

                    if (commit)
                    {
                        tx.Commit();
                        log.Information("COMMITTED — Orders row + queue message persisted");
                    }
                    else
                    {
                        tx.Rollback();
                        log.Warning("ROLLED BACK — neither Orders row nor queue message persisted");
                    }
                }
            }
        }
    }
}
