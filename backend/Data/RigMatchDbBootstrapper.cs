using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace RigMatch.Api.Data;

public static class RigMatchDbBootstrapper
{
    public static async Task InitializeAsync(RigMatchDbContext dbContext, CancellationToken cancellationToken = default)
    {
        await dbContext.Database.EnsureCreatedAsync(cancellationToken);
        await EnsureCvRecordSchemaAsync(dbContext, cancellationToken);
        await RoleCatalogSeeder.SeedAsync(dbContext, cancellationToken);
    }

    private static async Task EnsureCvRecordSchemaAsync(RigMatchDbContext dbContext, CancellationToken cancellationToken)
    {
        var connection = dbContext.Database.GetDbConnection();
        await connection.OpenAsync(cancellationToken);

        await using var command = connection.CreateCommand();
        command.CommandText = "PRAGMA table_info('CvRecords');";

        var hasFileHash = false;
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            if (string.Equals(reader["name"]?.ToString(), "FileHash", StringComparison.OrdinalIgnoreCase))
            {
                hasFileHash = true;
                break;
            }
        }

        await reader.CloseAsync();

        if (!hasFileHash)
        {
            await dbContext.Database.ExecuteSqlRawAsync(
                "ALTER TABLE CvRecords ADD COLUMN FileHash TEXT NOT NULL DEFAULT '';",
                cancellationToken);
        }

        await dbContext.Database.ExecuteSqlRawAsync(
            "CREATE INDEX IF NOT EXISTS IX_CvRecords_FileHash ON CvRecords(FileHash);",
            cancellationToken);

        if (!hasFileHash)
        {
            await BackfillMissingFileHashesAsync(dbContext, cancellationToken);
        }
    }

    private static async Task BackfillMissingFileHashesAsync(RigMatchDbContext dbContext, CancellationToken cancellationToken)
    {
        var records = await dbContext.CvRecords
            .Where(record => record.FileHash == string.Empty)
            .ToListAsync(cancellationToken);

        foreach (var record in records)
        {
            record.FileHash = $"legacy:{record.Id:N}";
        }

        if (records.Count > 0)
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
    }
}
