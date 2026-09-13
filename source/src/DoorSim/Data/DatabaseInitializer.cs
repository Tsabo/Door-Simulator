using Microsoft.Data.Sqlite;
using Tsabo.Data.Schema;
using Tsabo.Data.Schema.EntityFrameworkCore;
using Tsabo.Data.Schema.Sqlite;

namespace DoorSim.Data;

/// <summary>
/// Creates or updates the SQLite schema on startup via Tsabo.Data.Schema.
/// Additive-only — tables/columns are never dropped. No migration files required.
/// </summary>
public static class DatabaseInitializer
{
    public static async Task InitializeAsync(DoorSimDbContext db, ILogger logger,
        CancellationToken ct = default)
    {
        var connection = (SqliteConnection)db.Database.GetDbConnection();
        var shouldClose = connection.State != System.Data.ConnectionState.Open;
        if (shouldClose)
            await connection.OpenAsync(ct);

        try
        {
            var options = new SchemaOptions();
            var current = await new SqliteSchemaReader(connection, options).ReadAsync(ct);
            var expected = await new EFCoreSchemaReader(db).ReadAsync(ct);
            var diff = new SqliteSchemaComparer().Compare(current, expected, options);

            // SQLite cannot ALTER COLUMN. If the telemetry table has an outdated column type,
            // drop it so it is cleanly recreated.
            if (diff.Operations.Any(o => o.TableName.Equals("SimulationEvents", StringComparison.OrdinalIgnoreCase)
                                         && o.Type == MigrationOperationType.ModifyColumn))
            {
                logger.LogWarning("SimulationEvents schema has incompatible column types — rebuilding table");
                await using var dropCmd = connection.CreateCommand();
                dropCmd.CommandText = "DROP TABLE IF EXISTS \"SimulationEvents\"";
                await dropCmd.ExecuteNonQueryAsync(ct);

                current = await new SqliteSchemaReader(connection, options).ReadAsync(ct);
                diff = new SqliteSchemaComparer().Compare(current, expected, options);
            }

            if (!diff.HasChanges)
            {
                logger.LogInformation("Database schema is up to date");
                return;
            }

            var result = await new SqliteSchemaMigrator(connection, []).MigrateAsync(diff, options, ct);

            foreach (var warning in result.Warnings)
                logger.LogWarning("Schema: {Warning}", warning);

            if (result.Success)
                logger.LogInformation("Database schema initialized — {Count} statement(s) applied",
                    result.Operations.Count);
            else
                logger.LogError(result.Error, "Schema migration failed");
        }
        finally
        {
            if (shouldClose)
                await connection.CloseAsync();
        }
    }
}
