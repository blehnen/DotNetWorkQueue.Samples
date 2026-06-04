// Design: abstract-base (option a from PLAN-2.1).
// SqlServerInboxTestHelper and PostgreSqlInboxTestHelper (defined in their respective test files)
// subclass this and supply engine-specific connection factory + SQL strings.
// The configureConsumer Action<IConsumerQueue> parameter on RunConsumerAndWait lets the
// concrete subclass (or test class) call queue.Configuration.Options().EnableHoldTransactionUntilMessageCommitted = true
// from a context where the transport-specific namespace is imported — keeping this base transport-agnostic.
using System;
using System.Data.Common;
using System.Diagnostics;
using System.Threading;
using DotNetWorkQueue;
using DotNetWorkQueue.Configuration;
using DotNetWorkQueue.Messages;
using DotNetWorkQueue.Transport.RelationalDatabase;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SampleShared;
using Serilog;

namespace IntegrationTests
{
    /// <summary>
    /// DB-aware inbox test helper. Generic over the DNWQ transport (TInit/TCreation) so
    /// both SqlServer and PostgreSQL test classes reuse the same logic without duplicating ADO code.
    /// Subclasses supply the engine-specific seams:
    ///   CreateConnection, CreateOrdersProjectionTableSql, CountProjectionRowSql, DeleteProjectionRowByOrderIdSql.
    /// NOTE: no InsertProjectionRowSql — the handler INSERTs on the library-supplied transaction.
    /// </summary>
    public abstract class InboxTestHelper
    {
        protected readonly string _queueName;
        protected readonly string _connectionString;

        protected InboxTestHelper(string queueName, string connectionString)
        {
            _queueName = queueName;
            _connectionString = connectionString;

            Log.Logger = new LoggerConfiguration()
                .WriteTo.Console(
                    outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] [{SourceContext}] {Message:lj}{NewLine}{Exception}")
                .MinimumLevel.Debug()
                .CreateLogger();
        }

        // ---- Engine-specific seams (supplied by subclass) --------------------------------

        protected abstract DbConnection CreateConnection(string connectionString);

        protected abstract string CreateOrdersProjectionTableSql { get; }

        /// <summary>SELECT COUNT(*) ... WHERE OrderId = @OrderId</summary>
        protected abstract string CountProjectionRowSql { get; }

        /// <summary>DELETE ... WHERE OrderId = @OrderId</summary>
        protected abstract string DeleteProjectionRowByOrderIdSql { get; }

        /// <summary>
        /// Sets the message expiration on the supplied IAdditionalMessageData. SetExpiration is a
        /// TRANSPORT-SPECIFIC extension (lives in DotNetWorkQueue.Transport.SqlServer / .PostgreSQL),
        /// so this base — which can't import a transport namespace — delegates to the subclass.
        /// Implementations should call data.SetExpiration(TimeSpan.FromDays(1)) or similar; the
        /// queue was created with EnableMessageExpiration = true and the MetaData table requires it.
        /// </summary>
        protected abstract void ConfigureSeedExpiration(IAdditionalMessageData data);

        // NOTE: no InsertProjectionRowSql — the handler INSERTs on the library-supplied tx.

        // ---- Public API ------------------------------------------------------------------

        /// <summary>
        /// Applies the standard inbox consumer config shared by both transports' tests
        /// (worker count, heartbeat, message expiration). Caller MUST additionally set
        /// queue.Configuration.Options().EnableHoldTransactionUntilMessageCommitted = true
        /// from a context where the transport namespace is imported — Options() is a
        /// transport-specific extension and cannot be reached from this transport-agnostic base.
        /// </summary>
        public static void ApplyDefaultConsumerConfig(IConsumerQueue queue)
        {
            queue.Configuration.Worker.WorkerCount = 1;
            queue.Configuration.HeartBeat.UpdateTime = "*/10 * * * * *";
            queue.Configuration.HeartBeat.MonitorTime = TimeSpan.FromSeconds(15);
            queue.Configuration.HeartBeat.Time = TimeSpan.FromSeconds(35);
            queue.Configuration.MessageExpiration.Enabled = true;
            queue.Configuration.MessageExpiration.MonitorTime = TimeSpan.FromSeconds(20);
        }

        /// <summary>
        /// Ensures the OrdersProjection table exists. Idempotent; safe to call every test run.
        /// </summary>
        public void EnsureOrdersProjectionTable()
        {
            using (var conn = CreateConnection(_connectionString))
            {
                conn.Open();
                using (var cmd = conn.CreateCommand())
                {
                    cmd.CommandText = CreateOrdersProjectionTableSql;
                    cmd.ExecuteNonQuery();
                }
            }
        }

        /// <summary>
        /// Creates a fresh queue via QueueCreationContainer. Mirrors ProduceConsumeTestHelper lines 54-71.
        /// </summary>
        public void CreateQueue<TInit, TCreation>(Action<TCreation> configure)
            where TInit : class, ITransportInit, new()
            where TCreation : class, IQueueCreation
        {
            var queueConnection = new QueueConnection(_queueName, _connectionString);

            using (var createQueueContainer = new QueueCreationContainer<TInit>(
                serviceRegister => Injectors.AddInjectors(
                    Helpers.CreateForSerilog(),
                    SharedConfiguration.EnableTrace,
                    SharedConfiguration.EnableMetrics,
                    SharedConfiguration.EnableCompression,
                    SharedConfiguration.EnableEncryption,
                    "IntegrationTest", serviceRegister),
                options => Injectors.SetOptions(options, SharedConfiguration.EnableChaos)))
            {
                using (var createQueue = createQueueContainer.GetQueueCreation<TCreation>(queueConnection))
                {
                    configure(createQueue);
                    var result = createQueue.CreateQueue();
                    Assert.IsTrue(result.Success, $"Queue creation failed: {result.Status}");
                }
            }
        }

        /// <summary>
        /// Seeds a message via plain IProducerQueue&lt;OrderCreatedEvent&gt;.Send — NO transaction,
        /// NO outbox producer cast. Decouples inbox-test failures from outbox-test failures.
        /// Mirrors ProduceConsumeTestHelper lines 73-98 adapted to OrderCreatedEvent.
        /// </summary>
        public void SeedMessage<TInit>(OrderCreatedEvent msg)
            where TInit : class, ITransportInit, new()
        {
            var queueConnection = new QueueConnection(_queueName, _connectionString);

            using (var queueContainer = new QueueContainer<TInit>(
                serviceRegister => Injectors.AddInjectors(
                    Helpers.CreateForSerilog(),
                    SharedConfiguration.EnableTrace,
                    SharedConfiguration.EnableMetrics,
                    SharedConfiguration.EnableCompression,
                    SharedConfiguration.EnableEncryption,
                    "IntegrationTest", serviceRegister),
                options => Injectors.SetOptions(options, SharedConfiguration.EnableChaos)))
            {
                using (var queue = queueContainer.CreateProducer<OrderCreatedEvent>(queueConnection))
                {
                    // The queue was created with EnableMessageExpiration = true, so the MetaData
                    // table's ExpirationTime column is NOT NULL. The producer must supply expiration
                    // via IAdditionalMessageData or the INSERT fails. SetExpiration is transport-
                    // specific (per-transport extension namespace), so the subclass supplies it.
                    var data = new AdditionalMessageData();
                    ConfigureSeedExpiration(data);
                    IQueueOutputMessage result = queue.Send(msg, data);
                    Assert.IsFalse(result.HasError, $"Seed failed: {result.SendingException?.Message}");
                }
            }
        }

        /// <summary>
        /// Creates the consumer, applies configureConsumer (which MUST set
        /// EnableHoldTransactionUntilMessageCommitted = true BEFORE Start is called here),
        /// starts with InboxMessageProcessing.HandleMessages, and waits for the bounded timeout.
        /// </summary>
        /// <param name="configureConsumer">
        /// Callback supplied by the test; sets transport-specific Options() + Worker/HeartBeat config.
        /// Called on the consumer queue BEFORE queue.Start is invoked.
        /// </param>
        /// <param name="timeout">Upper bound on the wait — handler runs in worker threads.</param>
        /// <param name="completionPredicate">
        /// Optional: when supplied, the method polls (every 100 ms) and returns as soon as the
        /// predicate returns true, OR the timeout elapses. Use this for the COMMIT path (predicate
        /// `() => ProjectionRowCount(orderId) > 0`) so the test finishes in well under the timeout
        /// once the handler succeeds. Leave null for the ROLLBACK path — there's no positive
        /// "nothing happened" signal, so the test must consume the full window before asserting.
        /// </param>
        public void RunConsumerAndWait<TInit>(
            Action<IConsumerQueue> configureConsumer,
            TimeSpan timeout,
            Func<bool> completionPredicate = null)
            where TInit : class, ITransportInit, new()
        {
            var queueConnection = new QueueConnection(_queueName, _connectionString);

            using (var queueContainer = new QueueContainer<TInit>(
                serviceRegister => Injectors.AddInjectors(
                    Helpers.CreateForSerilog(),
                    SharedConfiguration.EnableTrace,
                    SharedConfiguration.EnableMetrics,
                    SharedConfiguration.EnableCompression,
                    SharedConfiguration.EnableEncryption,
                    "IntegrationTest", serviceRegister),
                options => Injectors.SetOptions(options, SharedConfiguration.EnableChaos)))
            {
                using (var queue = queueContainer.CreateConsumer(queueConnection))
                {
                    // configureConsumer sets EnableHoldTransactionUntilMessageCommitted = true
                    // (and worker/heartbeat options) BEFORE Start.
                    configureConsumer(queue);
                    queue.Start<OrderCreatedEvent>(InboxMessageProcessing.HandleMessages, CreateNotifications.Create(Log.Logger));

                    if (completionPredicate == null)
                    {
                        Thread.Sleep(timeout);
                    }
                    else
                    {
                        var sw = Stopwatch.StartNew();
                        while (sw.Elapsed < timeout)
                        {
                            if (completionPredicate())
                            {
                                return;
                            }
                            Thread.Sleep(100);
                        }
                    }
                }
                // Dispose stops the consumer.
            }
        }

        /// <summary>
        /// Returns the count of OrdersProjection rows matching orderId, using a SEPARATE connection
        /// (independent of any prior transaction) to verify commit/rollback semantics.
        /// </summary>
        public int ProjectionRowCount(Guid orderId)
        {
            using (var conn = CreateConnection(_connectionString))
            {
                conn.Open();
                using (var cmd = conn.CreateCommand())
                {
                    cmd.CommandText = CountProjectionRowSql;
                    var p = cmd.CreateParameter();
                    p.ParameterName = "@OrderId";
                    p.Value = orderId;
                    cmd.Parameters.Add(p);
                    return Convert.ToInt32(cmd.ExecuteScalar());
                }
            }
        }

        /// <summary>
        /// Removes the queue. Wrapped in try-catch so cleanup never fails the test run.
        /// </summary>
        public void RemoveQueue<TInit, TCreation>()
            where TInit : class, ITransportInit, new()
            where TCreation : class, IQueueCreation
        {
            try
            {
                var queueConnection = new QueueConnection(_queueName, _connectionString);

                using (var createQueueContainer = new QueueCreationContainer<TInit>(
                    serviceRegister => Injectors.AddInjectors(
                        Helpers.CreateForSerilog(),
                        SharedConfiguration.EnableTrace,
                        SharedConfiguration.EnableMetrics,
                        SharedConfiguration.EnableCompression,
                        SharedConfiguration.EnableEncryption,
                        "IntegrationTest", serviceRegister),
                    options => Injectors.SetOptions(options, SharedConfiguration.EnableChaos)))
                {
                    using (var createQueue = createQueueContainer.GetQueueCreation<TCreation>(queueConnection))
                    {
                        createQueue.RemoveQueue();
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[RemoveQueue] Warning: {ex.Message}");
            }
        }

        /// <summary>
        /// Deletes the OrdersProjection row for orderId so reruns stay clean.
        /// Does NOT drop the OrdersProjection table — DDL is idempotent and shared across test runs.
        /// </summary>
        public void DropProjectionRow(Guid orderId)
        {
            try
            {
                using (var conn = CreateConnection(_connectionString))
                {
                    conn.Open();
                    using (var cmd = conn.CreateCommand())
                    {
                        cmd.CommandText = DeleteProjectionRowByOrderIdSql;
                        var p = cmd.CreateParameter();
                        p.ParameterName = "@OrderId";
                        p.Value = orderId;
                        cmd.Parameters.Add(p);
                        cmd.ExecuteNonQuery();
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[DropProjectionRow] Warning: {ex.Message}");
            }
        }
    }
}
