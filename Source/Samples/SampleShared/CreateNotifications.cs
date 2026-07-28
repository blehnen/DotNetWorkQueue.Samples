using DotNetWorkQueue.Notifications;
using DotNetWorkQueue.Queue;
using Serilog;

namespace SampleShared
{
    public static class CreateNotifications
    {
        public static ConsumerQueueNotifications Create(ILogger logger)
        {
            var notifications =
                new ConsumerQueueNotifications((notification) => OnError(logger, notification),
                    (notification) => OnReceiveMessageError(logger, notification),
                    (notification) => OnMessageMovedToErrorQueue(logger, notification),
                    (notification) => OnPoisonMessage(logger, notification),
                    (notification) => OnMessageRollBack(logger, notification),
                    (notification) => OnMessageCompleted(logger, notification));
            return notifications;
        }
        //Message templates use named placeholders rather than interpolation, so the sink stores
        //MessageId as a queryable property instead of a single flattened string.
        //
        //Every notification's Error is an Exception, so it goes through the exception parameter
        //rather than becoming a message argument. That keeps the stack trace and any inner
        //exceptions intact - formatting it into the message would flatten it to its ToString().
        private static void OnMessageCompleted(ILogger log, MessageCompleteNotification obj)
        {
            log.Information("Processing completed {MessageId}", obj.MessageId);
        }

        private static void OnMessageRollBack(ILogger log, RollBackNotification obj)
        {
            log.Warning(obj.Error, "Processing has triggered a rollback for {MessageId}", obj.MessageId);
        }

        private static void OnPoisonMessage(ILogger log, PoisonMessageNotification obj)
        {
            log.Error(obj.Error, "Processing has triggered a poison message for {MessageId}", obj.MessageId);
        }

        private static void OnMessageMovedToErrorQueue(ILogger log, ErrorNotification obj)
        {
            log.Error(obj.Error, "Processing has failed for {MessageId}", obj.MessageId);
        }

        private static void OnReceiveMessageError(ILogger log, ErrorReceiveNotification obj)
        {
            log.Error(obj.Error, "Processing has failed to dequeue a message");
        }

        private static void OnError(ILogger log, ErrorNotification obj)
        {
            log.Error(obj.Error, "Processing has failed");
        }
    }
}
