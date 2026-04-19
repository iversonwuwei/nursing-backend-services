using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using NursingBackend.BuildingBlocks.Persistence;

namespace NursingBackend.Services.Rooms;

public sealed class RoomsDesignTimeDbContextFactory : IDesignTimeDbContextFactory<RoomsDbContext>
{
	public RoomsDbContext CreateDbContext(string[] args)
	{
		var builder = new DbContextOptionsBuilder<RoomsDbContext>();
		builder.UseNpgsql(PostgresConnectionStrings.Resolve(
			Environment.GetEnvironmentVariable("ConnectionStrings__RoomsPostgres"),
			Environment.GetEnvironmentVariable("ConnectionStrings__Postgres"),
			"nursing_rooms"));
		return new RoomsDbContext(builder.Options);
	}
}