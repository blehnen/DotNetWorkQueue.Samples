using DotNetWorkQueue;
using DotNetWorkQueue.Configuration;
using DotNetWorkQueue.Transport.PostgreSQL;
using DotNetWorkQueue.Transport.PostgreSQL.Basic;
using Npgsql;
using SampleShared;
using Serilog;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.IO;

namespace PostgreSQLConsumerInbox
{
    class Program
    {
        static void Main(string[] args)
        {
            //we are using serilog for sample purposes
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

            // Ensure the projection table exists before we start consuming
            EnsureOrdersProjectionTable(connectionString, log);

            //verify that the queue exists
            using (var createQueueContainer = new QueueCreationContainer<PostgreSqlMessageQueueInit>(serviceRegister =>
                Injectors.AddInjectors(Helpers.CreateForSerilog(), SharedConfiguration.EnableTrace, SharedConfiguration.EnableMetrics, SharedConfiguration.EnableCompression, SharedConfiguration.EnableEncryption, "PostgreSqlConsumerInbox", serviceRegister)
                , options => Injectors.SetOptions(options, SharedConfiguration.EnableChaos)))
            {
                using (var createQueue =
                    createQueueContainer.GetQueueCreation<PostgreSqlMessageQueueCreation>(queueConnection))
                {
                    if (!createQueue.QueueExists)
                    {
                        // Do NOT log the connection string here — it contains credentials.
                        Log.Error("Queue '{QueueName}' does not exist. Run PostgreSQLProducerOutbox first; it creates the queue and seeds OrderCreatedEvent messages.", queueName);
                        return;
                    }
                }
            }

#if NET8_0_OR_GREATER
            var dashboardClient = Injectors.StartDashboardRegistration(queueName, "PostgreSQLConsumerInbox");
#endif
            using (var queueContainer = new QueueContainer<PostgreSqlMessageQueueInit>(serviceRegister =>
                Injectors.AddInjectors(Helpers.CreateForSerilog(), SharedConfiguration.EnableTrace, SharedConfiguration.EnableMetrics, SharedConfiguration.EnableCompression, SharedConfiguration.EnableEncryption, "PostgreSqlConsumerInbox", serviceRegister)
                , options => Injectors.SetOptions(options, SharedConfiguration.EnableChaos)))
            {
                using (var queue = queueContainer.CreateConsumer(queueConnection))
                {
                    queue.Configuration.Worker.WorkerCount = 4;
                    queue.Configuration.HeartBeat.UpdateTime = "*/10 * * * * *";
                    queue.Configuration.HeartBeat.MonitorTime = TimeSpan.FromSeconds(15);
                    queue.Configuration.HeartBeat.Time = TimeSpan.FromSeconds(35);

                    queue.Configuration.TransportConfiguration.RetryDelayBehavior.Add(typeof(InvalidDataException), new List<TimeSpan> { TimeSpan.FromSeconds(3), TimeSpan.FromSeconds(6), TimeSpan.FromSeconds(9) });

                    queue.Configuration.MessageExpiration.Enabled = true;
                    queue.Configuration.MessageExpiration.MonitorTime = TimeSpan.FromSeconds(20);

                    // INBOX REQUIREMENT — must be set BEFORE queue.Start; enables the IRelationalWorkerNotification capability cast.
                    queue.Configuration.Options().EnableHoldTransactionUntilMessageCommitted = true;

                    queue.Start<OrderCreatedEvent>(InboxMessageProcessing.HandleMessages, CreateNotifications.Create(log));
                    Helpers.WaitForCancelKeyPress();
                }
            }
#if NET8_0_OR_GREATER
            Injectors.StopDashboardRegistration(dashboardClient);
#endif

            //if jaeger is using udp, sometimes the messages get lost; there doesn't seem to be a flush() call ?
            if (SharedConfiguration.EnableTrace)
                System.Threading.Thread.Sleep(2000);
        }

        private static void EnsureOrdersProjectionTable(string connectionString, ILogger log)
        {
            using (var conn = new NpgsqlConnection(connectionString))
            {
                conn.Open();
                using (var cmd = conn.CreateCommand())
                {
                    cmd.CommandText = OrdersProjectionDdl.PostgreSql;
                    cmd.ExecuteNonQuery();
                }
            }
            log.Information("OrdersProjection table ensured");
        }
    }
}
