using System;
using System.Data.Common;
using System.IO;
using DotNetWorkQueue;
using DotNetWorkQueue.Transport.PostgreSQL;
using DotNetWorkQueue.Transport.PostgreSQL.Basic;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Npgsql;
using SampleShared;

namespace IntegrationTests
{
    // Concrete subclass that supplies PostgreSQL-specific ADO seams.
    file sealed class PostgreSqlInboxTestHelper : InboxTestHelper
    {
        public PostgreSqlInboxTestHelper(string queueName, string connectionString)
            : base(queueName, connectionString)
        {
        }

        protected override DbConnection CreateConnection(string connectionString) =>
            new NpgsqlConnection(connectionString);

        // Unquoted identifiers — PG folds to lowercase; consistent with PLAN-1.2 / Phase-3 PG outbox.
        protected override string CreateOrdersProjectionTableSql => OrdersProjectionDdl.PostgreSql;

        protected override string CountProjectionRowSql =>
            "SELECT COUNT(*) FROM OrdersProjection WHERE OrderId = @OrderId";

        protected override string DeleteProjectionRowByOrderIdSql =>
            "DELETE FROM OrdersProjection WHERE OrderId = @OrderId";

        protected override void ConfigureSeedExpiration(IAdditionalMessageData data) =>
            data.SetExpiration(TimeSpan.FromDays(1));
    }

    [TestClass]
    public class PostgreSqlInboxTests
    {
        private InboxTestHelper _helper;
        private Guid _orderId;

        [TestInitialize]
        public void Setup()
        {
            var appConfigPath = Path.GetFullPath(Path.Combine(
                AppContext.BaseDirectory, "..", "..", "..", "..",
                "PostgreSQL", "PostgreSQLConsumerInbox", "App.config"));
            var connectionString = TestHelpers.ReadAppConfigValue(appConfigPath, "Database");
            var queueName = $"inbox_test_{Guid.NewGuid():N}";
            _orderId = Guid.NewGuid();

            _helper = new PostgreSqlInboxTestHelper(queueName, connectionString);
            _helper.EnsureOrdersProjectionTable();
            _helper.CreateQueue<PostgreSqlMessageQueueInit, PostgreSqlMessageQueueCreation>(createQueue =>
            {
                createQueue.Options.EnableDelayedProcessing = true;
                createQueue.Options.EnableHeartBeat = true;
                createQueue.Options.EnableMessageExpiration = true;
                createQueue.Options.EnableStatus = true;
                createQueue.Options.EnableStatusTable = true;
                createQueue.Options.EnableHistory = false;
            });
        }

        [TestMethod, TestCategory("LocalOnly")]
        public void Commit_WritesProjectionRow()
        {
            var msg = new OrderCreatedEvent
            {
                OrderId = _orderId,
                Customer = "Inbox Commit Test",
                Amount = 19.99m,
                CreatedUtc = DateTime.UtcNow,
                ForceRollback = false
            };
            _helper.SeedMessage<PostgreSqlMessageQueueInit>(msg);

            _helper.RunConsumerAndWait<PostgreSqlMessageQueueInit>(
                queue =>
                {
                    InboxTestHelper.ApplyDefaultConsumerConfig(queue);
                    queue.Configuration.Options().EnableHoldTransactionUntilMessageCommitted = true;
                },
                timeout: TimeSpan.FromSeconds(10));

            Assert.AreEqual(1, _helper.ProjectionRowCount(_orderId),
                "OrdersProjection row must exist after the consumer commits (handler did not throw).");
        }

        [TestMethod, TestCategory("LocalOnly")]
        public void Rollback_NoProjectionRow()
        {
            var msg = new OrderCreatedEvent
            {
                OrderId = _orderId,
                Customer = "Inbox Rollback Test",
                Amount = 19.99m,
                CreatedUtc = DateTime.UtcNow,
                ForceRollback = true
            };
            _helper.SeedMessage<PostgreSqlMessageQueueInit>(msg);

            _helper.RunConsumerAndWait<PostgreSqlMessageQueueInit>(
                queue =>
                {
                    InboxTestHelper.ApplyDefaultConsumerConfig(queue);
                    queue.Configuration.Options().EnableHoldTransactionUntilMessageCommitted = true;
                    // RetryDelayBehavior for InvalidOperationException: large delays so the handler
                    // does not keep re-firing during the poll window after it throws once. The library
                    // rolls back dequeue + projection write atomically on the FIRST throw — that's the
                    // observable we assert (RESEARCH.md §G.6 resolution).
                    queue.Configuration.TransportConfiguration.RetryDelayBehavior.Add(
                        typeof(InvalidOperationException),
                        new System.Collections.Generic.List<TimeSpan> {
                            TimeSpan.FromMinutes(5), TimeSpan.FromMinutes(10), TimeSpan.FromMinutes(15) });
                },
                timeout: TimeSpan.FromSeconds(10));

            Assert.AreEqual(0, _helper.ProjectionRowCount(_orderId),
                "OrdersProjection row must NOT exist after the consumer rolls back (handler threw because ForceRollback=true).");
        }

        [TestCleanup]
        public void Cleanup()
        {
            _helper?.DropProjectionRow(_orderId);
            _helper?.RemoveQueue<PostgreSqlMessageQueueInit, PostgreSqlMessageQueueCreation>();
        }
    }
}
