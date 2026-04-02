using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using NursingBackend.Services.Billing;
using NursingBackend.Services.Care;
using NursingBackend.Services.Elder;
using NursingBackend.Services.Health;
using NursingBackend.Services.Notification;
using NursingBackend.Services.Visit;

var builder = Host.CreateApplicationBuilder(args);
var connectionString = builder.Configuration.GetConnectionString("Postgres") ?? "Host=localhost;Port=5432;Database=nursing_platform;Username=nursing;Password=nursing";

builder.Services.AddLogging(logging => logging.AddSimpleConsole());
builder.Services.AddDbContext<ElderDbContext>(options => options.UseNpgsql(connectionString));
builder.Services.AddDbContext<HealthDbContext>(options => options.UseNpgsql(connectionString));
builder.Services.AddDbContext<CareDbContext>(options => options.UseNpgsql(connectionString));
builder.Services.AddDbContext<VisitDbContext>(options => options.UseNpgsql(connectionString));
builder.Services.AddDbContext<BillingDbContext>(options => options.UseNpgsql(connectionString));
builder.Services.AddDbContext<NotificationDbContext>(options => options.UseNpgsql(connectionString));

using var host = builder.Build();
using var scope = host.Services.CreateScope();
var logger = scope.ServiceProvider.GetRequiredService<ILoggerFactory>().CreateLogger("DatabaseMigrator");

await MigrateAsync<ElderDbContext>(scope.ServiceProvider, logger, "elder");
await MigrateAsync<HealthDbContext>(scope.ServiceProvider, logger, "health");
await MigrateAsync<CareDbContext>(scope.ServiceProvider, logger, "care");
await MigrateAsync<VisitDbContext>(scope.ServiceProvider, logger, "visit");
await MigrateAsync<BillingDbContext>(scope.ServiceProvider, logger, "billing");
await MigrateAsync<NotificationDbContext>(scope.ServiceProvider, logger, "notification");

logger.LogInformation("All database migrations applied successfully.");

static async Task MigrateAsync<TContext>(IServiceProvider serviceProvider, ILogger logger, string name)
	where TContext : DbContext
{
	var context = serviceProvider.GetRequiredService<TContext>();
	logger.LogInformation("Applying migrations for {ContextName}.", name);
	await context.Database.MigrateAsync();
	logger.LogInformation("Migrations completed for {ContextName}.", name);
}