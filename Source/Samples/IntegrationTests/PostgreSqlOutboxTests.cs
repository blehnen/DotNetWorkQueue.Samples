using System;
using System.Data.Common;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using DotNetWorkQueue.Transport.PostgreSQL;
using DotNetWorkQueue.Transport.PostgreSQL.Basic;
using Npgsql;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace IntegrationTests
{
    // Concrete subclass that supplies PostgreSQL-specific ADO seams.
    file sealed class PostgreSqlOutboxTestHelper : OutboxTestHelper
    {
        public PostgreSqlOutboxTestHelper(string queueName, string connectionString)
            : base(queueName, connectionString)
        {
        }

        protected override DbConnection CreateConnection(string connectionString)
        {
            return new NpgsqlConnection(connectionString);
        }

        protected override string CreateOrdersTableSql =>
            @"CREATE TABLE IF NOT EXISTS Orders (
    Id         SERIAL        PRIMARY KEY,
    OrderId    UUID          NOT NULL,
    Customer   TEXT          NOT NULL,
    Amount     NUMERIC(18,2) NOT NULL,
    CreatedUtc TIMESTAMPTZ   NOT NULL DEFAULT now()
)";

        protected override string InsertOrderSql =>
            "INSERT INTO Orders (OrderId, Customer, Amount, CreatedUtc) VALUES (@OrderId, @Customer, @Amount, @CreatedUtc)";

        protected override string CountOrderSql =>
            "SELECT COUNT(*) FROM Orders WHERE OrderId = @OrderId";

        protected override string DeleteOrderByIdSql =>
            "DELETE FROM Orders WHERE OrderId = @OrderId";
    }

    [TestClass]
    public class PostgreSqlOutboxTests
    {
        private OutboxTestHelper _helper;
        private Guid _orderId;

        [TestInitialize]
        public void Setup()
        {
            var appConfigPath = Path.GetFullPath(Path.Combine(
                AppContext.BaseDirectory,
                "..", "..", "..", "..",
                "PostgreSQL", "PostgreSQLProducerOutbox", "App.config"));
            var connectionString = TestHelpers.ReadAppConfigValue(appConfigPath, "Database");
            var queueName = $"outbox_test_{Guid.NewGuid():N}";
            _orderId = Guid.NewGuid();
            _helper = new PostgreSqlOutboxTestHelper(queueName, connectionString);
            _helper.EnsureOrdersTable();
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

        [TestMethod]
        [TestCategory("LocalOnly")]
        public void Commit_PersistsOrderRowAndQueueMessage()
        {
            _helper.RunOutbox<PostgreSqlMessageQueueInit>(_orderId, commit: true);
            Assert.AreEqual(1, _helper.OrderRowCount(_orderId), "Orders row must exist after commit");
            Assert.IsTrue(_helper.WaitingCount<PostgreSqlMessageQueueInit>() >= 1, "queue must hold the committed message");
        }

        [TestMethod]
        [TestCategory("LocalOnly")]
        public void Rollback_PersistsNothing()
        {
            _helper.RunOutbox<PostgreSqlMessageQueueInit>(_orderId, commit: false);
            Assert.AreEqual(0, _helper.OrderRowCount(_orderId), "no Orders row after rollback");
            Assert.AreEqual(0, _helper.WaitingCount<PostgreSqlMessageQueueInit>(), "queue must be empty after rollback");
        }

        [TestCleanup]
        public void Cleanup()
        {
            _helper?.DropOrdersRow(_orderId);
            _helper?.RemoveQueue<PostgreSqlMessageQueueInit, PostgreSqlMessageQueueCreation>();
        }
    }
}
