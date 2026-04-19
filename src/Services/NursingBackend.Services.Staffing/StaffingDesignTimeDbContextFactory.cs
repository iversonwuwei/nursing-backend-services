using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using NursingBackend.BuildingBlocks.Persistence;

namespace NursingBackend.Services.Staffing;

public sealed class StaffingDesignTimeDbContextFactory : IDesignTimeDbContextFactory<StaffingDbContext>
{
	public StaffingDbContext CreateDbContext(string[] args)
	{
		var builder = new DbContextOptionsBuilder<StaffingDbContext>();
		builder.UseNpgsql(PostgresConnectionStrings.Resolve(
			Environment.GetEnvironmentVariable("ConnectionStrings__StaffingPostgres"),
			Environment.GetEnvironmentVariable("ConnectionStrings__Postgres"),
			"nursing_staffing"));
		return new StaffingDbContext(builder.Options);
	}
}