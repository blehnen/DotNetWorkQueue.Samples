using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using DotNetWorkQueue;
using DotNetWorkQueue.Logging;
using Microsoft.Extensions.Logging;

namespace SampleShared
{
    public static class MessageProcessing
    {
        private static readonly ConcurrentDictionary<string, int> RetryErrorCount = new ConcurrentDictionary<string, int>();

        public static void HandleMessages(IReceivedMessage<SimpleMessage> arg1, IWorkerNotification arg2)
        {
            arg2.Log.LogInformation("Processing message {MessageId} - Processing time is {ProcessingTime}",
                arg1.MessageId.Id.Value, arg1.Body.ProcessingTime);

            SimulateConfiguredFault(arg1, arg2);
            WaitForProcessing(arg1, arg2);

            arg2.Log.LogInformation("Message {MessageId} complete", arg1.MessageId.Id.Value);
        }

        /// <summary>
        /// Reproduces whatever failure mode the message asked for, so the samples can show how the
        /// queue reacts to each one. A message with <see cref="ErrorTypes.None"/> just falls through.
        /// </summary>
        private static void SimulateConfiguredFault(IReceivedMessage<SimpleMessage> message, IWorkerNotification notification)
        {
            switch (message.Body.Error)
            {
                case ErrorTypes.Error:
                    //simulate some processing
                    System.Threading.Thread.Sleep(100);

                    //simulate an unexpected fault part-way through processing. Unlike the explicit
                    //throws below, this stands in for a latent bug in the handler rather than a
                    //validation failure - the queue treats it the same way either.
                    throw new DivideByZeroException("simulated processing failure");

                case ErrorTypes.RetryableErrorFail:
                    LogPreviousErrors(message, notification);

                    //simulate some processing
                    System.Threading.Thread.Sleep(100);
                    throw new InvalidDataException("the data is invalid. We will retry a few times and then give up because this error will happen over and over");

                case ErrorTypes.RetryableError:
                    SimulateRetryableError(message, notification);
                    break;
            }
        }

        /// <summary>
        /// Fails the first few attempts and then succeeds, so the retry delay behaviour is visible.
        /// </summary>
        private static void SimulateRetryableError(IReceivedMessage<SimpleMessage> message, IWorkerNotification notification)
        {
            //simulate some processing
            System.Threading.Thread.Sleep(100);

            var messageId = message.MessageId.Id.Value.ToString();
            if (!RetryErrorCount.TryGetValue(messageId, out var attempts))
            {
                RetryErrorCount.TryAdd(messageId, 1);
                throw new InvalidDataException("the data is invalid");
            }

            LogPreviousErrors(message, notification);

            //enough attempts - let this one through
            if (attempts > 2)
                return;

            RetryErrorCount[messageId] = attempts + 1;
            throw new InvalidDataException("the data is invalid");
        }

        private static void LogPreviousErrors(IReceivedMessage<SimpleMessage> message, IWorkerNotification notification)
        {
            foreach (var error in message.PreviousErrors)
            {
                notification.Log.LogInformation("previous error {PreviousError}, count {PreviousErrorCount}",
                    error.Key, error.Value);
            }
        }

        /// <summary>
        /// Stands in for the actual work. On transports that can roll back we wait on the
        /// cancellation token instead of sleeping, so a cancel can requeue the message.
        /// </summary>
        private static void WaitForProcessing(IReceivedMessage<SimpleMessage> message, IWorkerNotification notification)
        {
            if (!notification.TransportSupportsRollback)
            {
                System.Threading.Thread.Sleep(message.Body.ProcessingTime);
                return;
            }

            //MessageCancellation.Token is linked to the worker-level tokens, so it fires when:
            // - The worker is stopping (graceful shutdown)
            // - A per-message cancel is requested (e.g. from the dashboard)
            var canceled =
                notification.MessageCancellation.Token.WaitHandle.WaitOne(
                    TimeSpan.FromMilliseconds(message.Body.ProcessingTime));

            if (canceled) throw new OperationCanceledException("Processing was canceled"); //force a requeue
        }
    }
}
