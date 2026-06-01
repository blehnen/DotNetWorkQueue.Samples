using System;
using System.Configuration;
using System.Data.Common;
using System.Linq;
using DotNetWorkQueue;
using DotNetWorkQueue.Configuration;
using DotNetWorkQueue.Transport.PostgreSQL;
using DotNetWorkQueue.Transport.PostgreSQL.Basic;
using DotNetWorkQueue.Transport.RelationalDatabase;
using Npgsql;
using SampleShared;
using Serilog;

namespace PostgreSQLProducerOutbox
{
    class Program
    {
        static void Main(string[] args)
        {
            //we are using serilog for sample purposes.
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

            //create the container for creating a new queue
            using (var createQueueContainer = new QueueCreationContainer<PostgreSqlMessageQueueInit>(serviceRegister =>
                Injectors.AddInjectors(Helpers.CreateForSerilog(), SharedConfiguration.EnableTrace, SharedConfiguration.EnableMetrics, SharedConfiguration.EnableCompression, SharedConfiguration.EnableEncryption, "PostgreSqlProducerOutbox", serviceRegister)
                , options => Injectors.SetOptions(options, SharedConfiguration.EnableChaos)))
            {
                using (var createQueue =
                    createQueueContainer.GetQueueCreation<PostgreSqlMessageQueueCreation>(queueConnection))
                {
                    //Create the queue if it doesn't exist
                    if (!createQueue.QueueExists)
                    {
                        //queue options
                        createQueue.Options.EnableDelayedProcessing = true;
                        createQueue.Options.EnableHeartBeat = true;
                        createQueue.Options.EnableMessageExpiration = true;
                        createQueue.Options.EnableStatus = true;
                        createQueue.Options.EnableStatusTable = true;
                        createQueue.Options.EnableHistory = SharedConfiguration.EnableHistory;
                        var result = createQueue.CreateQueue();
                        log.Information(result.Status.ToString());
                    }
                    else log.Information("Queue already exists; not creating");
                }
            }

            //create the producer
            using (var queueContainer = new QueueContainer<PostgreSqlMessageQueueInit>(serviceRegister =>
                Injectors.AddInjectors(Helpers.CreateForSerilog(), SharedConfiguration.EnableTrace, SharedConfiguration.EnableMetrics, SharedConfiguration.EnableCompression, SharedConfiguration.EnableEncryption, "PostgreSqlProducerOutbox", serviceRegister)
                , options => Injectors.SetOptions(options, SharedConfiguration.EnableChaos)))
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

            //if jaeger is using udp, sometimes the messages get lost; there doesn't seem to be a flush() call ?
            if (SharedConfiguration.EnableTrace)
                System.Threading.Thread.Sleep(2000);
        }

        private static void EnsureOrdersTable(string connectionString, ILogger log)
        {
            using (var conn = new NpgsqlConnection(connectionString))
            {
                conn.Open();
                using (var cmd = conn.CreateCommand())
                {
                    cmd.CommandText =
                        @"CREATE TABLE IF NOT EXISTS Orders (
  Id SERIAL PRIMARY KEY,
  OrderId UUID NOT NULL,
  Customer TEXT NOT NULL,
  Amount NUMERIC(18,2) NOT NULL,
  CreatedUtc TIMESTAMPTZ NOT NULL DEFAULT now()
);";
                    cmd.ExecuteNonQuery();
                }
            }
            log.Information("Orders table ensured");
        }

        private static void RunOutboxLoop(IProducerQueue<OrderCreatedEvent> queue, string connectionString, IAdminApi admin, ILogger log)
        {
            var keepRunning = true;
            while (keepRunning)
            {
                var waiting = admin.Count(admin.Connections.Keys.FirstOrDefault(), QueueStatusAdmin.Waiting);
                log.Information("Items waiting in queue: {Count}", waiting);

                Console.WriteLine("c) Commit");
                Console.WriteLine("r) Rollback");
                Console.WriteLine("q) Quit");

                var key = char.ToLower(Console.ReadKey(true).KeyChar);
                switch (key)
                {
                    case 'c':
                        SendOnTransaction(queue, connectionString, true, log);
                        break;
                    case 'r':
                        SendOnTransaction(queue, connectionString, false, log);
                        break;
                    case 'q':
                        keepRunning = false;
                        break;
                }

                if (keepRunning)
                {
                    var afterWaiting = admin.Count(admin.Connections.Keys.FirstOrDefault(), QueueStatusAdmin.Waiting);
                    log.Information("Items waiting in queue after operation: {Count}", afterWaiting);
                }
            }
        }

        private static void SendOnTransaction(IProducerQueue<OrderCreatedEvent> queue, string connectionString, bool commit, ILogger log)
        {
            var order = new OrderCreatedEvent
            {
                OrderId = Guid.NewGuid(),
                Customer = "Sample Customer",
                Amount = 42.50m,
                CreatedUtc = DateTime.UtcNow
            };

            using (var conn = new NpgsqlConnection(connectionString))
            {
                conn.Open();
                using (var tx = conn.BeginTransaction())
                {
                    using (var cmd = conn.CreateCommand())
                    {
                        cmd.Transaction = tx;
                        cmd.CommandText = "INSERT INTO Orders (OrderId, Customer, Amount, CreatedUtc) VALUES (@OrderId, @Customer, @Amount, @CreatedUtc)";
                        cmd.Parameters.AddWithValue("OrderId", order.OrderId);
                        cmd.Parameters.AddWithValue("Customer", order.Customer);
                        cmd.Parameters.AddWithValue("Amount", order.Amount);
                        cmd.Parameters.AddWithValue("CreatedUtc", order.CreatedUtc);
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
