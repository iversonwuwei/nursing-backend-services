using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using NursingBackend.BuildingBlocks.Persistence;

namespace NursingBackend.Services.Care;

public sealed class CareDesignTimeDbContextFactory : IDesignTimeDbContextFactory<CareDbContext>
{
	public CareDbContext CreateDbContext(string[] args)
	{
		var builder = new DbContextOptionsBuilder<CareDbContext>();
		builder.UseNpgsql(PostgresConnectionStrings.Resolve(
			Environment.GetEnvironmentVariable("ConnectionStrings__CarePostgres"),
			Environment.GetEnvironmentVariable("ConnectionStrings__Postgres"),
			"nursing_care"));
		return new CareDbContext(builder.Options);
	}
}