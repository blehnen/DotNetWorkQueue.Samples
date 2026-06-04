using System;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using DotNetWorkQueue.Transport.Redis.Basic;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace IntegrationTests
{
    [TestClass]
    public class RedisTests
    {
        private ProduceConsumeTestHelper _helper;

        [TestInitialize]
        public void Setup()
        {
            var appConfigPath = Path.GetFullPath(Path.Combine(
                AppContext.BaseDirectory,
                "..", "..", "..", "..",
                "Redis", "RedisProducer", "App.config"));
            var connectionString = TestHelpers.ReadAppConfigValue(appConfigPath, "Database");
            var queueName = $"test_{Guid.NewGuid():N}";
            _helper = new ProduceConsumeTestHelper(queueName, connectionString, messageCount: 5);
        }

        [TestMethod]
        [TestCategory("LocalOnly")]
        public void ProduceConsume()
        {
            _helper.RunTest<RedisQueueInit, RedisQueueCreation>(createQueue =>
            {
                createQueue.Options.EnableHistory = false;
            });
        }

        [TestCleanup]
        public void Cleanup()
        {
            _helper?.RemoveQueue<RedisQueueInit, RedisQueueCreation>();
        }
    }
}
