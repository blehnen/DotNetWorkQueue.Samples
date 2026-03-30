using System;
using System.IO;
using DotNetWorkQueue.Transport.LiteDb.Basic;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace IntegrationTests
{
    [TestClass]
    public class LiteDbTests
    {
        private ProduceConsumeTestHelper _helper;
        private string _dbFilePath;

        [TestInitialize]
        public void Setup()
        {
            _dbFilePath = Path.Combine(Path.GetTempPath(), $"test_{Guid.NewGuid():N}.db");
            var queueName = $"test_{Guid.NewGuid():N}";
            var connectionString = $"Filename={_dbFilePath};Connection=shared;";
            _helper = new ProduceConsumeTestHelper(queueName, connectionString, messageCount: 5);
        }

        [TestMethod]
        [TestCategory("CI")]
        public void ProduceConsume()
        {
            _helper.RunTest<LiteDbMessageQueueInit, LiteDbMessageQueueCreation>(createQueue =>
            {
                createQueue.Options.EnableDelayedProcessing = true;
                createQueue.Options.EnableMessageExpiration = false;
                createQueue.Options.EnableStatusTable = true;
                createQueue.Options.EnableHistory = false;
            });
        }

        [TestCleanup]
        public void Cleanup()
        {
            _helper?.RemoveQueue<LiteDbMessageQueueInit, LiteDbMessageQueueCreation>();

            foreach (var suffix in new[] { "", "-journal", "-log" })
            {
                var path = _dbFilePath + suffix;
                try
                {
                    if (File.Exists(path))
                        File.Delete(path);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[Cleanup] Warning: could not delete {path}: {ex.Message}");
                }
            }
        }
    }
}
