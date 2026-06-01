using System;
using System.Data.Common;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using DotNetWorkQueue.Transport.SqlServer;
using DotNetWorkQueue.Transport.SqlServer.Basic;
using Microsoft.Data.SqlClient;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace IntegrationTests
{
    // Concrete subclass that supplies SQL Server–specific ADO seams.
    file sealed class SqlServerOutboxTestHelper : OutboxTestHelper
    {
        public SqlServerOutboxTestHelper(string queueName, string connectionString)
            : base(queueName, connectionString)
        {
        }

        protected override DbConnection CreateConnection(string connectionString)
        {
            return new SqlConnection(connectionString);
        }

        protected override string CreateOrdersTableSql =>
            @"IF NOT EXISTS (
    SELECT 1 FROM sys.tables t
    JOIN sys.schemas s ON t.schema_id = s.schema_id
    WHERE s.name = N'dbo' AND t.name = N'Orders'
)
BEGIN
    CREATE TABLE [dbo].[Orders] (
        [Id]         INT              IDENTITY(1,1) NOT NULL,
        [OrderId]    UNIQUEIDENTIFIER              NOT NULL,
        [Customer]   NVARCHAR(200)                 NOT NULL,
        [Amount]     DECIMAL(18,2)                 NOT NULL,
        [CreatedUtc] DATETIME2        DEFAULT SYSUTCDATETIME() NOT NULL,
        CONSTRAINT [PK_Orders] PRIMARY KEY CLUSTERED ([Id])
    );
END";

        protected override string InsertOrderSql =>
            "INSERT INTO [dbo].[Orders] ([OrderId],[Customer],[Amount],[CreatedUtc]) VALUES (@OrderId,@Customer,@Amount,@CreatedUtc)";

        protected override string CountOrderSql =>
            "SELECT COUNT(*) FROM [dbo].[Orders] WHERE [OrderId] = @OrderId";

        protected override string DeleteOrderByIdSql =>
            "DELETE FROM [dbo].[Orders] WHERE [OrderId] = @OrderId";
    }

    [TestClass]
    public class SqlServerOutboxTests
    {
        private OutboxTestHelper _helper;
        private Guid _orderId;

        [TestInitialize]
        public void Setup()
        {
            var appConfigPath = Path.GetFullPath(Path.Combine(
                AppContext.BaseDirectory,
                "..", "..", "..", "..",
                "SQLServer", "SQLServerProducerOutbox", "App.config"));
            var connectionString = TestHelpers.ReadAppConfigValue(appConfigPath, "Database");
            var queueName = $"outbox_test_{Guid.NewGuid():N}";
            _orderId = Guid.NewGuid();
            _helper = new SqlServerOutboxTestHelper(queueName, connectionString);
            _helper.EnsureOrdersTable();
            _helper.CreateQueue<SqlServerMessageQueueInit, SqlServerMessageQueueCreation>(createQueue =>
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
            _helper.RunOutbox<SqlServerMessageQueueInit>(_orderId, commit: true);
            Assert.AreEqual(1, _helper.OrderRowCount(_orderId), "Orders row must exist after commit");
            Assert.IsTrue(_helper.WaitingCount<SqlServerMessageQueueInit>() >= 1, "queue must hold the committed message");
        }

        [TestMethod]
        [TestCategory("LocalOnly")]
        public void Rollback_PersistsNothing()
        {
            _helper.RunOutbox<SqlServerMessageQueueInit>(_orderId, commit: false);
            Assert.AreEqual(0, _helper.OrderRowCount(_orderId), "no Orders row after rollback");
            Assert.AreEqual(0, _helper.WaitingCount<SqlServerMessageQueueInit>(), "queue must be empty after rollback");
        }

        [TestCleanup]
        public void Cleanup()
        {
            _helper?.DropOrdersRow(_orderId);
            _helper?.RemoveQueue<SqlServerMessageQueueInit, SqlServerMessageQueueCreation>();
        }
    }
}
