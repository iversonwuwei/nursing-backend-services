using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace NursingBackend.Services.Config;

public sealed class ConfigDesignTimeDbContextFactory : IDesignTimeDbContextFactory<ConfigDbContext>
{
    public ConfigDbContext CreateDbContext(string[] args)
    {
        var builder = new DbContextOptionsBuilder<ConfigDbContext>();
        builder.UseNpgsql("Host=localhost;Port=5432;Database=nursing_platform;Username=nursing;Password=nursing");
        return new ConfigDbContext(builder.Options);
    }
}
