using DotNetWorkQueue;
using DotNetWorkQueue.Configuration;
using Serilog;

namespace SampleShared
{
    /// <summary>
    /// Brings an existing queue's schema forward.
    /// </summary>
    public static class SchemaUpgrade
    {
        /// <summary>
        /// Upgrades the queue's schema if it is behind, and does nothing if it is current.
        /// </summary>
        /// <remarks>
        /// From 0.14.0 a producer or consumer refuses to start against a SQL Server, PostgreSQL or
        /// SQLite queue created by an older version, throwing QueueSchemaOutOfDateException. The
        /// samples only create a queue when it does not already exist, so a queue left over from an
        /// earlier run is exactly the case that needs this.
        ///
        /// A PostgreSQL queue written before 0.12.0 also needs the time zone its timestamps were
        /// written in, which nothing in the database records. Set it on the connection's additional
        /// settings with SetUpgradeSourceTimeZone before calling this. The upgrade refuses to guess,
        /// because guessing wrong shifts every timestamp, so it fails with a message saying so.
        /// </remarks>
        public static void BringForward<TInit>(QueueConnection queueConnection, ILogger log)
            where TInit : class, ITransportInit, new()
        {
            using (var container = new QueueContainer<TInit>())
            using (var admin = container.CreateAdminContainer(queueConnection))
            {
                var result = admin.GetInstance<IQueueSchemaVersion>().UpgradeSchema();

                if (result.Success)
                {
                    log.Information("Schema {Status}: version {From} to {To}",
                        result.Status, result.StartingVersion, result.EndingVersion);
                }
                else
                {
                    log.Error("Schema upgrade failed ({Status}): {Error}",
                        result.Status, result.ErrorMessage);
                }
            }
        }
    }
}
