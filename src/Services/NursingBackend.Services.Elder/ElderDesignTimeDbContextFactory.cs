using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using NursingBackend.BuildingBlocks.Persistence;

namespace NursingBackend.Services.Elder;

public sealed class ElderDesignTimeDbContextFactory : IDesignTimeDbContextFactory<ElderDbContext>
{
	public ElderDbContext CreateDbContext(string[] args)
	{
		var builder = new DbContextOptionsBuilder<ElderDbContext>();
		builder.UseNpgsql(PostgresConnectionStrings.Resolve(
			Environment.GetEnvironmentVariable("ConnectionStrings__ElderPostgres"),
			Environment.GetEnvironmentVariable("ConnectionStrings__Postgres"),
			"nursing_elder"));
		return new ElderDbContext(builder.Options);
	}
}