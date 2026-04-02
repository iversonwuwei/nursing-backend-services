using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace NursingBackend.Services.Visit;

public sealed class VisitDesignTimeDbContextFactory : IDesignTimeDbContextFactory<VisitDbContext>
{
	public VisitDbContext CreateDbContext(string[] args)
	{
		var builder = new DbContextOptionsBuilder<VisitDbContext>();
		builder.UseNpgsql("Host=localhost;Port=5432;Database=nursing_platform;Username=nursing;Password=nursing");
		return new VisitDbContext(builder.Options);
	}
}