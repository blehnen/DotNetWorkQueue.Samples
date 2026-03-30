using System;
using System.IO;
using DotNetWorkQueue.Transport.SQLite.Basic;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace IntegrationTests
{
    [TestClass]
    public class SqliteTests
    {
        private ProduceConsumeTestHelper _helper;
        private string _dbFilePath;

        [TestInitialize]
        public void Setup()
        {
            _dbFilePath = Path.Combine(Path.GetTempPath(), $"test_{Guid.NewGuid():N}.db3");
            var queueName = $"test_{Guid.NewGuid():N}";
            var connectionString = $"Data Source={_dbFilePath};Version=3;";
            _helper = new ProduceConsumeTestHelper(queueName, connectionString, messageCount: 5);
        }

        [TestMethod]
        [TestCategory("CI")]
        public void ProduceConsume()
        {
            _helper.RunTest<SqLiteMessageQueueInit, SqLiteMessageQueueCreation>(createQueue =>
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
            _helper?.RemoveQueue<SqLiteMessageQueueInit, SqLiteMessageQueueCreation>();

            // Remove the SQLite db file and any journal/WAL side-cars.
            foreach (var suffix in new[] { "", "-journal", "-wal", "-shm" })
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
