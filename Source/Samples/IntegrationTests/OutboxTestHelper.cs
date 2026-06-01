// Design: abstract-base (option a from PLAN-2.1).
// SqlServerOutboxTestHelper and PostgreSqlOutboxTestHelper (defined in their respective test files)
// subclass this and supply engine-specific connection factory + SQL strings.
using System;
using System.Data.Common;
using System.Linq;
using DotNetWorkQueue;
using DotNetWorkQueue.Configuration;
using DotNetWorkQueue.Transport.RelationalDatabase;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Serilog;
using SampleShared;

namespace IntegrationTests
{
    /// <summary>
    /// DB-aware outbox test helper. Generic over the DNWQ transport (TInit/TCreation) so
    /// both SqlServer and PostgreSQL test classes reuse the same logic without duplicating ADO code.
    /// Subclasses supply the engine-specific seams:
    ///   CreateConnection, CreateOrdersTableSql, InsertOrderSql, CountOrderSql, DeleteOrderSql.
    /// </summary>
    public abstract class OutboxTestHelper
    {
        protected readonly string _queueName;
        protected readonly string _connectionString;

        protected OutboxTestHelper(string queueName, string connectionString)
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

        protected abstract string CreateOrdersTableSql { get; }

        protected abstract string InsertOrderSql { get; }

        protected abstract string CountOrderSql { get; }

        protected abstract string DeleteOrderByIdSql { get; }

        // ---- Public API ------------------------------------------------------------------

        /// <summary>
        /// Ensures the Orders table exists. Idempotent; safe to call every test run.
        /// </summary>
        public void EnsureOrdersTable()
        {
            using (var conn = CreateConnection(_connectionString))
            {
                conn.Open();
                using (var cmd = conn.CreateCommand())
                {
                    cmd.CommandText = CreateOrdersTableSql;
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
                    "OutboxTest", serviceRegister),
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
        /// Core outbox unit under test: opens a business connection, begins a transaction,
        /// INSERTs an Orders row, sends the message on the same transaction, then commits or rolls back.
        /// </summary>
        public void RunOutbox<TInit>(Guid orderId, bool commit)
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
                    "OutboxTest", serviceRegister),
                options => Injectors.SetOptions(options, SharedConfiguration.EnableChaos)))
            {
                using (var queue = queueContainer.CreateProducer<OrderCreatedEvent>(queueConnection))
                {
                    var order = new OrderCreatedEvent
                    {
                        OrderId = orderId,
                        Customer = "Outbox Test",
                        Amount = 99.99m,
                        CreatedUtc = DateTime.UtcNow
                    };

                    using (var conn = CreateConnection(_connectionString))
                    {
                        conn.Open();
                        // DbTransaction (not var): the Send overload binds to DbTransaction; inferring SqlTransaction/NpgsqlTransaction would compile here but break the Program.cs callers, so keep the base type to mirror the contract.
                        using (DbTransaction tx = conn.BeginTransaction())
                        {
                            using (var cmd = conn.CreateCommand())
                            {
                                cmd.Transaction = tx;
                                cmd.CommandText = InsertOrderSql;
                                var pOrderId = cmd.CreateParameter();
                                pOrderId.ParameterName = "@OrderId";
                                pOrderId.Value = order.OrderId;
                                cmd.Parameters.Add(pOrderId);
                                var pCustomer = cmd.CreateParameter();
                                pCustomer.ParameterName = "@Customer";
                                pCustomer.Value = order.Customer;
                                cmd.Parameters.Add(pCustomer);
                                var pAmount = cmd.CreateParameter();
                                pAmount.ParameterName = "@Amount";
                                pAmount.Value = order.Amount;
                                cmd.Parameters.Add(pAmount);
                                var pCreatedUtc = cmd.CreateParameter();
                                pCreatedUtc.ParameterName = "@CreatedUtc";
                                pCreatedUtc.Value = order.CreatedUtc;
                                cmd.Parameters.Add(pCreatedUtc);
                                cmd.ExecuteNonQuery();
                            }

                            Assert.IsTrue(
                                queue is IRelationalProducerQueue<OrderCreatedEvent>,
                                "producer must support IRelationalProducerQueue");
                            var relational = (IRelationalProducerQueue<OrderCreatedEvent>)queue;
                            relational.Send(order, tx);

                            if (commit)
                            {
                                tx.Commit();
                            }
                            else
                            {
                                tx.Rollback();
                            }
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Returns the queue's Waiting message count via IAdminApi.
        /// Opens a fresh QueueContainer so it reads the current DB state after commit/rollback.
        /// </summary>
        public long WaitingCount<TInit>()
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
                    "OutboxTest", serviceRegister),
                options => Injectors.SetOptions(options, SharedConfiguration.EnableChaos)))
            {
                using (var admin = queueContainer.CreateAdminApi())
                {
                    admin.AddQueueConnection(queueContainer, queueConnection);
                    var connId = admin.Connections.Keys.FirstOrDefault();
                    return Convert.ToInt64(admin.Count(connId, QueueStatusAdmin.Waiting));
                }
            }
        }

        /// <summary>
        /// Returns the count of Orders rows matching orderId, using a SEPARATE connection
        /// (independent of any prior transaction) to verify commit/rollback semantics.
        /// </summary>
        public int OrderRowCount(Guid orderId)
        {
            using (var conn = CreateConnection(_connectionString))
            {
                conn.Open();
                using (var cmd = conn.CreateCommand())
                {
                    cmd.CommandText = CountOrderSql;
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
                        "OutboxTest", serviceRegister),
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
        /// Deletes the Orders row for orderId so reruns stay clean.
        /// Does NOT drop the table (DDL is idempotent and shared across tests).
        /// </summary>
        public void DropOrdersRow(Guid orderId)
        {
            try
            {
                using (var conn = CreateConnection(_connectionString))
                {
                    conn.Open();
                    using (var cmd = conn.CreateCommand())
                    {
                        cmd.CommandText = DeleteOrderByIdSql;
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
                Console.WriteLine($"[DropOrdersRow] Warning: {ex.Message}");
            }
        }
    }
}
