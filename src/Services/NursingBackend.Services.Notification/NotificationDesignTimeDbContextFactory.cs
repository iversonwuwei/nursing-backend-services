using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace NursingBackend.Services.Notification;

public sealed class NotificationDesignTimeDbContextFactory : IDesignTimeDbContextFactory<NotificationDbContext>
{
	public NotificationDbContext CreateDbContext(string[] args)
	{
		var builder = new DbContextOptionsBuilder<NotificationDbContext>();
		builder.UseNpgsql("Host=localhost;Port=5432;Database=nursing_platform;Username=nursing;Password=nursing");
		return new NotificationDbContext(builder.Options);
	}
}