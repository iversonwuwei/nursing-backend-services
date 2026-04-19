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
using NursingBackend.Services.Organization;
using NursingBackend.Services.Rooms;
using NursingBackend.Services.Staffing;
using NursingBackend.Services.Visit;
using Npgsql;

var builder = Host.CreateApplicationBuilder(args);
var elderConnectionString = PostgresConnectionStrings.Resolve(builder.Configuration, "ElderPostgres", "nursing_elder");
var healthConnectionString = PostgresConnectionStrings.Resolve(builder.Configuration, "HealthPostgres", "nursing_health");
var careConnectionString = PostgresConnectionStrings.Resolve(builder.Configuration, "CarePostgres", "nursing_care");
var visitConnectionString = PostgresConnectionStrings.Resolve(builder.Configuration, "VisitPostgres", "nursing_visit");
var billingConnectionString = PostgresConnectionStrings.Resolve(builder.Configuration, "BillingPostgres", "nursing_billing");
var notificationConnectionString = PostgresConnectionStrings.Resolve(builder.Configuration, "NotificationPostgres", "nursing_notification");
var operationsConnectionString = PostgresConnectionStrings.Resolve(builder.Configuration, "OperationsPostgres", "nursing_operations");
var organizationConnectionString = PostgresConnectionStrings.Resolve(builder.Configuration, "OrganizationsPostgres", "nursing_organizations");
var roomsConnectionString = PostgresConnectionStrings.Resolve(builder.Configuration, "RoomsPostgres", "nursing_rooms");
var staffingConnectionString = PostgresConnectionStrings.Resolve(builder.Configuration, "StaffingPostgres", "nursing_staffing");

builder.Services.AddLogging(logging => logging.AddSimpleConsole());
builder.Services.AddDbContext<ElderDbContext>(options => options.UseNpgsql(elderConnectionString));
builder.Services.AddDbContext<HealthDbContext>(options => options.UseNpgsql(healthConnectionString));
builder.Services.AddDbContext<CareDbContext>(options => options.UseNpgsql(careConnectionString));
builder.Services.AddDbContext<VisitDbContext>(options => options.UseNpgsql(visitConnectionString));
builder.Services.AddDbContext<BillingDbContext>(options => options.UseNpgsql(billingConnectionString));
builder.Services.AddDbContext<NotificationDbContext>(options => options.UseNpgsql(notificationConnectionString));
builder.Services.AddDbContext<OperationsDbContext>(options => options.UseNpgsql(operationsConnectionString));
builder.Services.AddDbContext<OrganizationDbContext>(options => options.UseNpgsql(organizationConnectionString));
builder.Services.AddDbContext<RoomsDbContext>(options => options.UseNpgsql(roomsConnectionString));
builder.Services.AddDbContext<StaffingDbContext>(options => options.UseNpgsql(staffingConnectionString));

using var host = builder.Build();
using var scope = host.Services.CreateScope();
var logger = scope.ServiceProvider.GetRequiredService<ILoggerFactory>().CreateLogger("DatabaseMigrator");

await EnsureDatabaseExistsAsync(elderConnectionString, logger, "elder");
await EnsureDatabaseExistsAsync(healthConnectionString, logger, "health");
await EnsureDatabaseExistsAsync(careConnectionString, logger, "care");
await EnsureDatabaseExistsAsync(visitConnectionString, logger, "visit");
await EnsureDatabaseExistsAsync(billingConnectionString, logger, "billing");
await EnsureDatabaseExistsAsync(notificationConnectionString, logger, "notification");
await EnsureDatabaseExistsAsync(operationsConnectionString, logger, "operations");
await EnsureDatabaseExistsAsync(organizationConnectionString, logger, "organization");
await EnsureDatabaseExistsAsync(roomsConnectionString, logger, "rooms");
await EnsureDatabaseExistsAsync(staffingConnectionString, logger, "staffing");

await MigrateAsync<ElderDbContext>(scope.ServiceProvider, logger, "elder");
await MigrateAsync<HealthDbContext>(scope.ServiceProvider, logger, "health");
await MigrateAsync<CareDbContext>(scope.ServiceProvider, logger, "care");
await MigrateAsync<VisitDbContext>(scope.ServiceProvider, logger, "visit");
await MigrateAsync<BillingDbContext>(scope.ServiceProvider, logger, "billing");
await MigrateAsync<NotificationDbContext>(scope.ServiceProvider, logger, "notification");
await MigrateAsync<OperationsDbContext>(scope.ServiceProvider, logger, "operations");
await MigrateAsync<OrganizationDbContext>(scope.ServiceProvider, logger, "organization");
await MigrateAsync<RoomsDbContext>(scope.ServiceProvider, logger, "rooms");
await MigrateAsync<StaffingDbContext>(scope.ServiceProvider, logger, "staffing");

logger.LogInformation("All database migrations applied successfully.");

static async Task EnsureDatabaseExistsAsync(string connectionString, ILogger logger, string name)
{
	var builder = new NpgsqlConnectionStringBuilder(connectionString);
	var databaseName = builder.Database;
	if (string.IsNullOrWhiteSpace(databaseName))
	{
		throw new InvalidOperationException($"Connection string for {name} does not specify a database.");
	}

	builder.Database = "postgres";
	await using var connection = new NpgsqlConnection(builder.ConnectionString);
	await connection.OpenAsync();

	await using var checkCommand = new NpgsqlCommand("SELECT 1 FROM pg_database WHERE datname = @databaseName", connection);
	checkCommand.Parameters.AddWithValue("databaseName", databaseName);
	var exists = await checkCommand.ExecuteScalarAsync() is not null;
	if (exists)
	{
		logger.LogInformation("Database already exists for {ContextName}: {DatabaseName}", name, databaseName);
		return;
	}

	var quotedDatabaseName = QuoteIdentifier(databaseName);
	await using var createCommand = new NpgsqlCommand($"CREATE DATABASE {quotedDatabaseName}", connection);
	await createCommand.ExecuteNonQueryAsync();
	logger.LogInformation("Created missing database for {ContextName}: {DatabaseName}", name, databaseName);
}

static async Task MigrateAsync<TContext>(IServiceProvider serviceProvider, ILogger logger, string name)
	where TContext : DbContext
{
	var context = serviceProvider.GetRequiredService<TContext>();
	logger.LogInformation("Applying migrations for {ContextName}.", name);
	await context.Database.MigrateAsync();
	logger.LogInformation("Migrations completed for {ContextName}.", name);
}

static string QuoteIdentifier(string value) => $"\"{value.Replace("\"", "\"\"")}\"";