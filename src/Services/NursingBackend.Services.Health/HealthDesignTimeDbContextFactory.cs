using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace NursingBackend.Services.Health;

public sealed class HealthDesignTimeDbContextFactory : IDesignTimeDbContextFactory<HealthDbContext>
{
	public HealthDbContext CreateDbContext(string[] args)
	{
		var builder = new DbContextOptionsBuilder<HealthDbContext>();
		builder.UseNpgsql("Host=localhost;Port=5432;Database=nursing_platform;Username=nursing;Password=nursing");
		return new HealthDbContext(builder.Options);
	}
}