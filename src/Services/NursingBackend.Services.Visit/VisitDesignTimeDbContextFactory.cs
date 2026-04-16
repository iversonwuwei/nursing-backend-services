using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using NursingBackend.BuildingBlocks.Persistence;

namespace NursingBackend.Services.Visit;

public sealed class VisitDesignTimeDbContextFactory : IDesignTimeDbContextFactory<VisitDbContext>
{
	public VisitDbContext CreateDbContext(string[] args)
	{
		var builder = new DbContextOptionsBuilder<VisitDbContext>();
		builder.UseNpgsql(PostgresConnectionStrings.Resolve(
			Environment.GetEnvironmentVariable("ConnectionStrings__VisitPostgres"),
			Environment.GetEnvironmentVariable("ConnectionStrings__Postgres"),
			"nursing_visit"));
		return new VisitDbContext(builder.Options);
	}
}