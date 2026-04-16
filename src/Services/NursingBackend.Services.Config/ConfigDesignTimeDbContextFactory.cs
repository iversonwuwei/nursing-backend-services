using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using NursingBackend.BuildingBlocks.Persistence;

namespace NursingBackend.Services.Config;

public sealed class ConfigDesignTimeDbContextFactory : IDesignTimeDbContextFactory<ConfigDbContext>
{
    public ConfigDbContext CreateDbContext(string[] args)
    {
        var builder = new DbContextOptionsBuilder<ConfigDbContext>();
        builder.UseNpgsql(PostgresConnectionStrings.Resolve(
            Environment.GetEnvironmentVariable("ConnectionStrings__ConfigPostgres"),
            Environment.GetEnvironmentVariable("ConnectionStrings__Postgres"),
            "nursing_config"));
        return new ConfigDbContext(builder.Options);
    }
}
