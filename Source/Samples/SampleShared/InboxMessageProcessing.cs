using System;
using System.Data.Common;
using DotNetWorkQueue;
using DotNetWorkQueue.Transport.RelationalDatabase;
using Microsoft.Extensions.Logging;

namespace SampleShared
{
    public static class InboxMessageProcessing
    {
        /// <summary>
        /// Sync inbox handler — persists via cmd.ExecuteNonQuery().
        /// Matches Action&lt;IReceivedMessage&lt;OrderCreatedEvent&gt;, IWorkerNotification&gt;.
        /// </summary>
        public static void HandleMessages(IReceivedMessage<OrderCreatedEvent> arg1, IWorkerNotification arg2)
        {
            Persist(arg1, arg2, useAsyncAdo: false);
        }

        /// <summary>
        /// Async-ADO inbox handler — persists via cmd.ExecuteNonQueryAsync().GetAwaiter().GetResult().
        /// Same delegate shape as HandleMessages; NOT async void — queue.Start&lt;T&gt; accepts only Action&lt;…&gt;.
        /// </summary>
        public static void HandleMessagesAsync(IReceivedMessage<OrderCreatedEvent> arg1, IWorkerNotification arg2)
        {
            Persist(arg1, arg2, useAsyncAdo: true);
        }

        private static void Persist(IReceivedMessage<OrderCreatedEvent> arg1, IWorkerNotification arg2, bool useAsyncAdo)
        {
            // Fail loud on misconfiguration: cast only succeeds when EnableHoldTransactionUntilMessageCommitted = true
            // on a relational transport. A failed cast means the inbox pattern cannot be exercised.
            if (arg2 is not IRelationalWorkerNotification relational)
            {
                throw new InvalidOperationException(
                    "The inbox sample requires a relational transport with EnableHoldTransactionUntilMessageCommitted = true.");
            }

            // ForceRollback demo: throw BEFORE the INSERT so that nothing is written.
            // The library rolls back the dequeue and the projection write together.
            if (arg1.Body.ForceRollback)
            {
                throw new InvalidOperationException(
                    "ForceRollback requested — throwing so the dequeue and the projection write both roll back.");
            }

            // Build the command on the library-owned transaction using only System.Data.Common types.
            // IMPORTANT: do NOT call Commit, Rollback, or Dispose on relational.Transaction or its Connection —
            // the library owns the transaction and commits/rolls back atomically on handler return or throw.
            // A using on the command itself is fine.
            using (DbCommand cmd = relational.Transaction.Connection.CreateCommand())
            {
                cmd.Transaction = relational.Transaction;
                cmd.CommandText =
                    "INSERT INTO OrdersProjection (OrderId, Customer, Amount, CreatedUtc) VALUES (@OrderId, @Customer, @Amount, @CreatedUtc)";

                // @-named parameters work for both SqlClient and Npgsql.
                // ForceRollback is a control field — NOT persisted.
                AddParameter(cmd, "@OrderId", arg1.Body.OrderId);
                AddParameter(cmd, "@Customer", arg1.Body.Customer);
                AddParameter(cmd, "@Amount", arg1.Body.Amount);
                AddParameter(cmd, "@CreatedUtc", arg1.Body.CreatedUtc);

                if (useAsyncAdo)
                {
                    cmd.ExecuteNonQueryAsync().GetAwaiter().GetResult();
                }
                else
                {
                    cmd.ExecuteNonQuery();
                }
            }

            arg2.Log.LogInformation("Inbox: persisted OrderId {OrderId} to OrdersProjection.", arg1.Body.OrderId);
        }

        private static void AddParameter(DbCommand cmd, string name, object value)
        {
            var p = cmd.CreateParameter();
            p.ParameterName = name;
            p.Value = value;
            cmd.Parameters.Add(p);
        }
    }
}
