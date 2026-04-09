using System.Data;
using Microsoft.EntityFrameworkCore;

namespace NursingBackend.Services.Config;

internal static class ConfigDatabaseBootstrapper
{
    public static async Task EnsureSchemaAsync(ConfigDbContext dbContext, CancellationToken cancellationToken = default)
    {
        if (await HasStaticTextsTableAsync(dbContext, cancellationToken))
        {
            return;
        }

        var createScript = dbContext.Database.GenerateCreateScript();
        if (string.IsNullOrWhiteSpace(createScript))
        {
            return;
        }

        await dbContext.Database.ExecuteSqlRawAsync(createScript, cancellationToken);
    }

    private static async Task<bool> HasStaticTextsTableAsync(ConfigDbContext dbContext, CancellationToken cancellationToken)
    {
        var connection = dbContext.Database.GetDbConnection();
        if (connection.State != ConnectionState.Open)
        {
            await connection.OpenAsync(cancellationToken);
        }

        await using var command = connection.CreateCommand();
        command.CommandText = """
            select exists (
                select 1
                from information_schema.tables
                where table_schema = current_schema()
                  and table_name = 'StaticTexts'
            )
            """;

        var result = await command.ExecuteScalarAsync(cancellationToken);
        return result is true;
    }
}