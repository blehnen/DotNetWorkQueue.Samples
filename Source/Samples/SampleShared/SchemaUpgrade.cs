using System;
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
        /// because guessing wrong shifts every timestamp, so it fails and this throws.
        /// </remarks>
        /// <exception cref="InvalidOperationException">The upgrade did not succeed.</exception>
        public static void BringForward<TInit>(QueueConnection queueConnection, ILogger log, string appName)
            where TInit : class, ITransportInit, new()
        {
            //the same registrations the samples give their other containers, so the library's own
            //logging and tracing during the upgrade goes where the rest of the sample's does
            using (var container = new QueueContainer<TInit>(
                       serviceRegister => Injectors.AddInjectors(Helpers.CreateForSerilog(),
                           SharedConfiguration.EnableTrace, SharedConfiguration.EnableMetrics,
                           SharedConfiguration.EnableCompression, SharedConfiguration.EnableEncryption,
                           appName, serviceRegister),
                       options => Injectors.SetOptions(options, SharedConfiguration.EnableChaos)))
            using (var admin = container.CreateAdminContainer(queueConnection))
            {
                //Redis and LiteDb have no versioned schema, so nothing registers an updater
                var updater = admin.TryGetInstance<IQueueSchemaVersion>();
                if (updater == null)
                {
                    log.Information("This transport has no schema versioning; nothing to upgrade");
                    return;
                }

                var result = updater.UpgradeSchema();
                if (result.Success)
                {
                    log.Information("Schema {Status}: version {From} to {To}",
                        result.Status, result.StartingVersion, result.EndingVersion);
                    return;
                }

                //returning here would let the producer start and fail on its own with
                //QueueSchemaOutOfDateException, which says less and says it further from the cause
                throw new InvalidOperationException(
                    $"Schema upgrade failed ({result.Status}): {result.ErrorMessage}");
            }
        }
    }
}
