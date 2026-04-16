using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using NursingBackend.BuildingBlocks.Persistence;

namespace NursingBackend.Services.Health;

public sealed class HealthDesignTimeDbContextFactory : IDesignTimeDbContextFactory<HealthDbContext>
{
	public HealthDbContext CreateDbContext(string[] args)
	{
		var builder = new DbContextOptionsBuilder<HealthDbContext>();
		builder.UseNpgsql(PostgresConnectionStrings.Resolve(
			Environment.GetEnvironmentVariable("ConnectionStrings__HealthPostgres"),
			Environment.GetEnvironmentVariable("ConnectionStrings__Postgres"),
			"nursing_health"));
		return new HealthDbContext(builder.Options);
	}
}