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
        //Message templates use named placeholders rather than interpolation. The sink then stores
        //MessageId and Error as queryable properties instead of a single flattened string, and the
        //arguments are only formatted if the level is actually enabled.
        private static void OnMessageCompleted(ILogger log, MessageCompleteNotification obj)
        {
            log.Information("Processing completed {MessageId}", obj.MessageId);
        }

        private static void OnMessageRollBack(ILogger log, RollBackNotification obj)
        {
            log.Warning("Processing has triggered a rollback for {MessageId}: {Error}", obj.MessageId, obj.Error);
        }

        private static void OnPoisonMessage(ILogger log, PoisonMessageNotification obj)
        {
            log.Error("Processing has triggered a poison message for {MessageId}: {Error}", obj.MessageId, obj.Error);
        }

        private static void OnMessageMovedToErrorQueue(ILogger log, ErrorNotification obj)
        {
            log.Error("Processing has failed for {MessageId}: {Error}", obj.MessageId, obj.Error);
        }

        private static void OnReceiveMessageError(ILogger log, ErrorReceiveNotification obj)
        {
            log.Error("Processing has failed to dequeue a message: {Error}", obj.Error);
        }

        private static void OnError(ILogger log, ErrorNotification obj)
        {
            log.Error("Processing has failed: {Error}", obj.Error);
        }
    }
}
