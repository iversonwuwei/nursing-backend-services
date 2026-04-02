using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace NursingBackend.Services.Billing;

public sealed class BillingDesignTimeDbContextFactory : IDesignTimeDbContextFactory<BillingDbContext>
{
	public BillingDbContext CreateDbContext(string[] args)
	{
		var builder = new DbContextOptionsBuilder<BillingDbContext>();
		builder.UseNpgsql("Host=localhost;Port=5432;Database=nursing_platform;Username=nursing;Password=nursing");
		return new BillingDbContext(builder.Options);
	}
}