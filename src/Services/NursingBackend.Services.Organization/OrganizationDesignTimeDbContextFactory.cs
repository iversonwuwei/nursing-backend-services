using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using NursingBackend.BuildingBlocks.Persistence;

namespace NursingBackend.Services.Organization;

public sealed class OrganizationDesignTimeDbContextFactory : IDesignTimeDbContextFactory<OrganizationDbContext>
{
	public OrganizationDbContext CreateDbContext(string[] args)
	{
		var builder = new DbContextOptionsBuilder<OrganizationDbContext>();
		builder.UseNpgsql(PostgresConnectionStrings.Resolve(
			Environment.GetEnvironmentVariable("ConnectionStrings__OrganizationsPostgres"),
			Environment.GetEnvironmentVariable("ConnectionStrings__Postgres"),
			"nursing_organizations"));
		return new OrganizationDbContext(builder.Options);
	}
}