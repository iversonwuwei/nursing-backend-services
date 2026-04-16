using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using NursingBackend.BuildingBlocks.Persistence;
using NursingBackend.Services.Billing;
using NursingBackend.Services.Care;
using NursingBackend.Services.Elder;
using NursingBackend.Services.Health;
using NursingBackend.Services.Notification;
using NursingBackend.Services.Operations;
using NursingBackend.Services.Visit;

var builder = Host.CreateApplicationBuilder(args);
var elderConnectionString = PostgresConnectionStrings.Resolve(builder.Configuration, "ElderPostgres", "nursing_elder");
var healthConnectionString = PostgresConnectionStrings.Resolve(builder.Configuration, "HealthPostgres", "nursing_health");
var careConnectionString = PostgresConnectionStrings.Resolve(builder.Configuration, "CarePostgres", "nursing_care");
var visitConnectionString = PostgresConnectionStrings.Resolve(builder.Configuration, "VisitPostgres", "nursing_visit");
var billingConnectionString = PostgresConnectionStrings.Resolve(builder.Configuration, "BillingPostgres", "nursing_billing");
var notificationConnectionString = PostgresConnectionStrings.Resolve(builder.Configuration, "NotificationPostgres", "nursing_notification");
var operationsConnectionString = PostgresConnectionStrings.Resolve(builder.Configuration, "OperationsPostgres", "nursing_operations");

builder.Services.AddLogging(logging => logging.AddSimpleConsole());
builder.Services.AddDbContext<ElderDbContext>(options => options.UseNpgsql(elderConnectionString));
builder.Services.AddDbContext<HealthDbContext>(options => options.UseNpgsql(healthConnectionString));
builder.Services.AddDbContext<CareDbContext>(options => options.UseNpgsql(careConnectionString));
builder.Services.AddDbContext<VisitDbContext>(options => options.UseNpgsql(visitConnectionString));
builder.Services.AddDbContext<BillingDbContext>(options => options.UseNpgsql(billingConnectionString));
builder.Services.AddDbContext<NotificationDbContext>(options => options.UseNpgsql(notificationConnectionString));
builder.Services.AddDbContext<OperationsDbContext>(options => options.UseNpgsql(operationsConnectionString));

using var host = builder.Build();
using var scope = host.Services.CreateScope();
var logger = scope.ServiceProvider.GetRequiredService<ILoggerFactory>().CreateLogger("DatabaseMigrator");

await MigrateAsync<ElderDbContext>(scope.ServiceProvider, logger, "elder");
await MigrateAsync<HealthDbContext>(scope.ServiceProvider, logger, "health");
await MigrateAsync<CareDbContext>(scope.ServiceProvider, logger, "care");
await MigrateAsync<VisitDbContext>(scope.ServiceProvider, logger, "visit");
await MigrateAsync<BillingDbContext>(scope.ServiceProvider, logger, "billing");
await MigrateAsync<NotificationDbContext>(scope.ServiceProvider, logger, "notification");
await MigrateAsync<OperationsDbContext>(scope.ServiceProvider, logger, "operations");

logger.LogInformation("All database migrations applied successfully.");

static async Task MigrateAsync<TContext>(IServiceProvider serviceProvider, ILogger logger, string name)
	where TContext : DbContext
{
	var context = serviceProvider.GetRequiredService<TContext>();
	logger.LogInformation("Applying migrations for {ContextName}.", name);
	await context.Database.MigrateAsync();
	logger.LogInformation("Migrations completed for {ContextName}.", name);
}