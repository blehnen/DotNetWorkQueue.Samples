using System;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using DotNetWorkQueue;
using DotNetWorkQueue.Messages;
using DotNetWorkQueue.Transport.SqlServer;
using DotNetWorkQueue.Transport.SqlServer.Basic;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace IntegrationTests
{
    [TestClass]
    public class SqlServerTests
    {
        private ProduceConsumeTestHelper _helper;

        [TestInitialize]
        public void Setup()
        {
            var appConfigPath = Path.GetFullPath(Path.Combine(
                AppContext.BaseDirectory,
                "..", "..", "..", "..",
                "SQLServer", "SQLServerProducer", "App.config"));
            var connectionString = TestHelpers.ReadAppConfigValue(appConfigPath, "Database");
            var queueName = $"test_{Guid.NewGuid():N}";
            _helper = new ProduceConsumeTestHelper(queueName, connectionString, messageCount: 5);
        }

        [TestMethod]
        [TestCategory("LocalOnly")]
        public void ProduceConsume()
        {
            _helper.RunTest<SqlServerMessageQueueInit, SqlServerMessageQueueCreation>(createQueue =>
            {
                createQueue.Options.EnableDelayedProcessing = true;
                createQueue.Options.EnableHeartBeat = true;
                createQueue.Options.EnableMessageExpiration = true;
                createQueue.Options.EnableStatus = true;
                createQueue.Options.EnableStatusTable = true;
                createQueue.Options.EnableHistory = false;
            }, () =>
            {
                var data = new AdditionalMessageData();
                data.SetExpiration(TimeSpan.FromDays(1));
                return data;
            });
        }

        [TestCleanup]
        public void Cleanup()
        {
            _helper?.RemoveQueue<SqlServerMessageQueueInit, SqlServerMessageQueueCreation>();
        }
    }
}
