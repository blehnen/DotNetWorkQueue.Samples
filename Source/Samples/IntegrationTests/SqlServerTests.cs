using System;
using System.IO;
using System.Linq;
using System.Xml.Linq;
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
            var connectionString = ReadAppConfigValue(appConfigPath, "Database");
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
            });
        }

        [TestCleanup]
        public void Cleanup()
        {
            _helper?.RemoveQueue<SqlServerMessageQueueInit, SqlServerMessageQueueCreation>();
        }

        private static string ReadAppConfigValue(string appConfigPath, string key)
        {
            var doc = XDocument.Load(appConfigPath);
            var element = doc.Root?.Element("appSettings")?
                .Elements("add")
                .FirstOrDefault(e => e.Attribute("key")?.Value == key);
            return element?.Attribute("value")?.Value
                ?? throw new InvalidOperationException($"Key '{key}' not found in {appConfigPath}");
        }
    }
}
