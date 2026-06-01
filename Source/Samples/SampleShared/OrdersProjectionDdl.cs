namespace SampleShared
{
    /// <summary>
    /// OrdersProjection table DDL constants used by the inbox samples and their integration tests.
    /// Centralized here so the sample's startup table-creation and the test helper's verification
    /// schema cannot drift. Transport-agnostic: pure strings, no SqlClient/Npgsql dependency.
    /// </summary>
    public static class OrdersProjectionDdl
    {
        public const string SqlServer = @"
IF NOT EXISTS (
    SELECT 1 FROM sys.tables t
    JOIN sys.schemas s ON t.schema_id = s.schema_id
    WHERE s.name = N'dbo' AND t.name = N'OrdersProjection'
)
BEGIN
    CREATE TABLE [dbo].[OrdersProjection] (
        [Id]         INT              IDENTITY(1,1) NOT NULL,
        [OrderId]    UNIQUEIDENTIFIER              NOT NULL,
        [Customer]   NVARCHAR(200)                 NOT NULL,
        [Amount]     DECIMAL(18,2)                 NOT NULL,
        [CreatedUtc] DATETIME2        DEFAULT SYSUTCDATETIME() NOT NULL,
        CONSTRAINT [PK_OrdersProjection] PRIMARY KEY CLUSTERED ([Id])
    );
END";

        public const string PostgreSql = @"
CREATE TABLE IF NOT EXISTS OrdersProjection (
    Id         SERIAL        PRIMARY KEY,
    OrderId    UUID          NOT NULL,
    Customer   TEXT          NOT NULL,
    Amount     NUMERIC(18,2) NOT NULL,
    CreatedUtc TIMESTAMPTZ   NOT NULL DEFAULT now()
);";
    }
}
