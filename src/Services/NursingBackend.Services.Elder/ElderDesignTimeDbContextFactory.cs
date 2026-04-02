using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace NursingBackend.Services.Elder;

public sealed class ElderDesignTimeDbContextFactory : IDesignTimeDbContextFactory<ElderDbContext>
{
	public ElderDbContext CreateDbContext(string[] args)
	{
		var builder = new DbContextOptionsBuilder<ElderDbContext>();
		builder.UseNpgsql("Host=localhost;Port=5432;Database=nursing_platform;Username=nursing;Password=nursing");
		return new ElderDbContext(builder.Options);
	}
}