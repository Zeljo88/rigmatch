using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace RigMatch.Api.Data;

public static class RigMatchDbBootstrapper
{
    public static async Task InitializeAsync(RigMatchDbContext dbContext, CancellationToken cancellationToken = default)
    {
        await dbContext.Database.EnsureCreatedAsync(cancellationToken);
        await EnsureCvRecordSchemaAsync(dbContext, cancellationToken);
        await EnsureCompanyProjectSchemaAsync(dbContext, cancellationToken);
        await EnsureRoleAliasSchemaAsync(dbContext, cancellationToken);
        await EnsureSuggestedRoleAliasSchemaAsync(dbContext, cancellationToken);
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

    private static async Task EnsureRoleAliasSchemaAsync(RigMatchDbContext dbContext, CancellationToken cancellationToken)
    {
        var connection = dbContext.Database.GetDbConnection();
        if (connection.State != System.Data.ConnectionState.Open)
        {
            await connection.OpenAsync(cancellationToken);
        }

        await using var command = connection.CreateCommand();
        command.CommandText = "PRAGMA table_info('RoleAliases');";

        var hasRequiresReview = false;
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            if (string.Equals(reader["name"]?.ToString(), "RequiresReview", StringComparison.OrdinalIgnoreCase))
            {
                hasRequiresReview = true;
                break;
            }
        }

        await reader.CloseAsync();

        if (!hasRequiresReview)
        {
            await dbContext.Database.ExecuteSqlRawAsync(
                "ALTER TABLE RoleAliases ADD COLUMN RequiresReview INTEGER NOT NULL DEFAULT 0;",
                cancellationToken);
        }
    }

    private static async Task EnsureCompanyProjectSchemaAsync(RigMatchDbContext dbContext, CancellationToken cancellationToken)
    {
        await dbContext.Database.ExecuteSqlRawAsync("""
            CREATE TABLE IF NOT EXISTS CompanyProjects (
                Id TEXT NOT NULL CONSTRAINT PK_CompanyProjects PRIMARY KEY,
                CompanyId TEXT NOT NULL,
                Title TEXT NOT NULL,
                ClientName TEXT NOT NULL,
                PrimaryRole TEXT NOT NULL,
                AdditionalRolesJson TEXT NOT NULL,
                RequiredSkillsJson TEXT NOT NULL,
                PreferredSkillsJson TEXT NOT NULL,
                RequiredCertificationsJson TEXT NOT NULL,
                PreferredCertificationsJson TEXT NOT NULL,
                MinimumExperienceYears INTEGER NULL,
                Location TEXT NOT NULL,
                PreferredEducation TEXT NOT NULL,
                Description TEXT NOT NULL,
                Status TEXT NOT NULL,
                StartDateUtc TEXT NULL,
                CreatedAtUtc TEXT NOT NULL,
                UpdatedAtUtc TEXT NULL,
                FOREIGN KEY (CompanyId) REFERENCES Companies(Id) ON DELETE CASCADE
            );
            """, cancellationToken);

        await dbContext.Database.ExecuteSqlRawAsync("""
            CREATE INDEX IF NOT EXISTS IX_CompanyProjects_CompanyId
            ON CompanyProjects(CompanyId);
            """, cancellationToken);
    }

    private static async Task EnsureSuggestedRoleAliasSchemaAsync(RigMatchDbContext dbContext, CancellationToken cancellationToken)
    {
        await dbContext.Database.ExecuteSqlRawAsync("""
            CREATE TABLE IF NOT EXISTS SuggestedRoleAliases (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                CompanyId TEXT NOT NULL,
                LastCvRecordId TEXT NULL,
                StandardRoleId INTEGER NOT NULL,
                RawAlias TEXT NOT NULL,
                RawAliasNormalized TEXT NOT NULL,
                ConfirmationCount INTEGER NOT NULL DEFAULT 1,
                FirstSuggestedAtUtc TEXT NOT NULL,
                LastSuggestedAtUtc TEXT NOT NULL,
                FOREIGN KEY (CompanyId) REFERENCES Companies(Id) ON DELETE CASCADE,
                FOREIGN KEY (LastCvRecordId) REFERENCES CvRecords(Id) ON DELETE SET NULL,
                FOREIGN KEY (StandardRoleId) REFERENCES StandardRoles(Id) ON DELETE CASCADE
            );
            """, cancellationToken);

        await dbContext.Database.ExecuteSqlRawAsync("""
            CREATE UNIQUE INDEX IF NOT EXISTS IX_SuggestedRoleAliases_CompanyId_StandardRoleId_RawAliasNormalized
            ON SuggestedRoleAliases(CompanyId, StandardRoleId, RawAliasNormalized);
            """, cancellationToken);
    }
}
