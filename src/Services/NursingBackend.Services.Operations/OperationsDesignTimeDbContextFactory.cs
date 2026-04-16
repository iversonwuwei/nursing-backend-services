using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using NursingBackend.BuildingBlocks.Persistence;

namespace NursingBackend.Services.Operations;

public sealed class OperationsDesignTimeDbContextFactory : IDesignTimeDbContextFactory<OperationsDbContext>
{
	public OperationsDbContext CreateDbContext(string[] args)
	{
		var builder = new DbContextOptionsBuilder<OperationsDbContext>();
		builder.UseNpgsql(PostgresConnectionStrings.Resolve(
			Environment.GetEnvironmentVariable("ConnectionStrings__OperationsPostgres"),
			Environment.GetEnvironmentVariable("ConnectionStrings__Postgres"),
			"nursing_operations"));
		return new OperationsDbContext(builder.Options);
	}
}