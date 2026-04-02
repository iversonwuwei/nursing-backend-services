using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace NursingBackend.Services.Care;

public sealed class CareDesignTimeDbContextFactory : IDesignTimeDbContextFactory<CareDbContext>
{
	public CareDbContext CreateDbContext(string[] args)
	{
		var builder = new DbContextOptionsBuilder<CareDbContext>();
		builder.UseNpgsql("Host=localhost;Port=5432;Database=nursing_platform;Username=nursing;Password=nursing");
		return new CareDbContext(builder.Options);
	}
}