using System;
using System.Threading;
using DotNetWorkQueue;
using DotNetWorkQueue.Configuration;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Serilog;
using SampleShared;

namespace IntegrationTests
{
    /// <summary>
    /// Reusable helper that creates a queue, produces N messages, consumes them all,
    /// and asserts zero errors. Transport type and queue-creation options are
    /// supplied by the caller so the same logic works for every backend.
    /// </summary>
    public class ProduceConsumeTestHelper
    {
        private readonly string _queueName;
        private readonly string _connectionString;
        private readonly int _messageCount;
        private readonly TimeSpan _timeout;

        public ProduceConsumeTestHelper(
            string queueName,
            string connectionString,
            int messageCount = 5,
            TimeSpan? timeout = null)
        {
            _queueName = queueName;
            _connectionString = connectionString;
            _messageCount = messageCount;
            _timeout = timeout ?? TimeSpan.FromSeconds(30);
        }

        /// <summary>
        /// Full produce-consume round-trip test.
        /// </summary>
        public void RunTest<TInit, TCreation>(Action<TCreation> configureQueueOptions, Func<IAdditionalMessageData> createMessageData = null)
            where TInit : class, ITransportInit, new()
            where TCreation : class, IQueueCreation
        {
            // ---- 1. Serilog / ILoggerFactory ------------------------------------------------
            // Each using-block gets its own AddInjectors call; we must set Log.Logger first.
            var serilogLogger = new LoggerConfiguration()
                .WriteTo.Console(
                    outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] [{SourceContext}] {Message:lj}{NewLine}{Exception}")
                .MinimumLevel.Debug()
                .CreateLogger();
            Log.Logger = serilogLogger;

            var queueConnection = new QueueConnection(_queueName, _connectionString);

            // ---- 2. Create queue -----------------------------------------------------------
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
                    configureQueueOptions(createQueue);
                    var result = createQueue.CreateQueue();
                    Assert.IsTrue(result.Success,
                        $"Queue creation failed: {result.Status}");
                }
            }

            // ---- 3. Produce ----------------------------------------------------------------
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
                using (var queue = queueContainer.CreateProducer<SimpleMessage>(queueConnection))
                {
                    for (int i = 0; i < _messageCount; i++)
                    {
                        var message = Messages.CreateSimpleMessage(10, 0);
                        IQueueOutputMessage sendResult;
                        if (createMessageData != null)
                            sendResult = queue.Send(message, createMessageData());
                        else
                            sendResult = queue.Send(message);
                        Assert.IsFalse(sendResult.HasError,
                            $"Send failed for message {i}: {sendResult.SendingException?.Message}");
                    }
                }
            }

            // ---- 4. Consume ----------------------------------------------------------------
            int completed = 0;
            int errors = 0;
            int poisonMessages = 0;
            var allDone = new ManualResetEventSlim(false);

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
                    queue.Configuration.Worker.WorkerCount = 1;
                    queue.Configuration.HeartBeat.UpdateTime = "*/10 * * * * *";
                    queue.Configuration.HeartBeat.MonitorTime = TimeSpan.FromSeconds(15);
                    queue.Configuration.HeartBeat.Time = TimeSpan.FromSeconds(35);
                    queue.Configuration.MessageExpiration.Enabled = true;
                    queue.Configuration.MessageExpiration.MonitorTime = TimeSpan.FromSeconds(20);

                    var notifications = new DotNetWorkQueue.Queue.ConsumerQueueNotifications(
                        notification =>
                        {
                            Interlocked.Increment(ref errors);
                            serilogLogger.Error("Error: {Error}", notification.Error);
                        },
                        notification =>
                        {
                            Interlocked.Increment(ref errors);
                            serilogLogger.Error("Receive error: {Error}", notification.Error);
                        },
                        notification =>
                        {
                            Interlocked.Increment(ref errors);
                            serilogLogger.Error("Moved to error queue: {MessageId}", notification.MessageId);
                        },
                        notification =>
                        {
                            Interlocked.Increment(ref poisonMessages);
                            serilogLogger.Error("Poison message: {MessageId}", notification.MessageId);
                        },
                        notification =>
                        {
                            serilogLogger.Warning("Rollback: {MessageId}", notification.MessageId);
                        },
                        notification =>
                        {
                            int count = Interlocked.Increment(ref completed);
                            serilogLogger.Information("Completed {Count}/{Total}", count, _messageCount);
                            if (count >= _messageCount)
                                allDone.Set();
                        });

                    queue.Start<SimpleMessage>(MessageProcessing.HandleMessages, notifications);

                    bool signaled = allDone.Wait(_timeout);

                    // IConsumerQueue has no Stop() — Dispose (end of using block) stops it.
                    // Assert here while the queue is still alive so dispose races don't matter.
                    Assert.IsTrue(signaled,
                        $"Timed out after {_timeout}: completed={completed}/{_messageCount}, errors={errors}");
                    Assert.AreEqual(_messageCount, completed,
                        $"Expected {_messageCount} completions but got {completed}");
                    Assert.AreEqual(0, errors,
                        $"Expected 0 errors but got {errors}");
                    Assert.AreEqual(0, poisonMessages,
                        $"Expected 0 poison messages but got {poisonMessages}");
                }
            }
        }

        /// <summary>
        /// Removes the queue. Wrapped in try-catch so cleanup never fails the test.
        /// </summary>
        public void RemoveQueue<TInit, TCreation>()
            where TInit : class, ITransportInit, new()
            where TCreation : class, IQueueCreation
        {
            try
            {
                var serilogLogger = new LoggerConfiguration()
                    .WriteTo.Console()
                    .MinimumLevel.Warning()
                    .CreateLogger();
                Log.Logger = serilogLogger;

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
                // Log but do not rethrow — cleanup must not fail the test run.
                Console.WriteLine($"[RemoveQueue] Warning: {ex.Message}");
            }
        }
    }
}
