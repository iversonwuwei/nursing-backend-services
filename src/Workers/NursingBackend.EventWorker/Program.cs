using Microsoft.EntityFrameworkCore;
using NursingBackend.BuildingBlocks.Persistence;
using NursingBackend.EventWorker;
using NursingBackend.Services.Billing;
using NursingBackend.Services.Care;
using NursingBackend.Services.Notification;
using NursingBackend.Services.Visit;

var builder = Host.CreateApplicationBuilder(args);
var billingConnectionString = PostgresConnectionStrings.Resolve(builder.Configuration, "BillingPostgres", "nursing_billing");
var careConnectionString = PostgresConnectionStrings.Resolve(builder.Configuration, "CarePostgres", "nursing_care");
var visitConnectionString = PostgresConnectionStrings.Resolve(builder.Configuration, "VisitPostgres", "nursing_visit");
var notificationConnectionString = PostgresConnectionStrings.Resolve(builder.Configuration, "NotificationPostgres", "nursing_notification");

builder.Services.AddLogging(logging => logging.AddSimpleConsole());
builder.Services.AddDbContext<BillingDbContext>(options => options.UseNpgsql(billingConnectionString));
builder.Services.AddDbContext<CareDbContext>(options => options.UseNpgsql(careConnectionString));
builder.Services.AddDbContext<VisitDbContext>(options => options.UseNpgsql(visitConnectionString));
builder.Services.AddDbContext<NotificationDbContext>(options => options.UseNpgsql(notificationConnectionString));
builder.Services.Configure<RabbitMqOptions>(builder.Configuration.GetSection("RabbitMq"));
builder.Services.AddSingleton<WorkerMetrics>();
builder.Services.AddHostedService<OutboxPublisherWorker>();
builder.Services.AddHostedService<NotificationConsumerWorker>();
builder.Services.AddHostedService<QueueBacklogReporterWorker>();

var host = builder.Build();
host.Run();