using Microsoft.EntityFrameworkCore;
using NursingBackend.EventWorker;
using NursingBackend.Services.Billing;
using NursingBackend.Services.Care;
using NursingBackend.Services.Notification;
using NursingBackend.Services.Visit;

var builder = Host.CreateApplicationBuilder(args);
var connectionString = builder.Configuration.GetConnectionString("Postgres") ?? "Host=localhost;Port=5432;Database=nursing_platform;Username=nursing;Password=nursing";

builder.Services.AddLogging(logging => logging.AddSimpleConsole());
builder.Services.AddDbContext<BillingDbContext>(options => options.UseNpgsql(connectionString));
builder.Services.AddDbContext<CareDbContext>(options => options.UseNpgsql(connectionString));
builder.Services.AddDbContext<VisitDbContext>(options => options.UseNpgsql(connectionString));
builder.Services.AddDbContext<NotificationDbContext>(options => options.UseNpgsql(connectionString));
builder.Services.Configure<RabbitMqOptions>(builder.Configuration.GetSection("RabbitMq"));
builder.Services.AddSingleton<WorkerMetrics>();
builder.Services.AddHostedService<OutboxPublisherWorker>();
builder.Services.AddHostedService<NotificationConsumerWorker>();
builder.Services.AddHostedService<QueueBacklogReporterWorker>();

var host = builder.Build();
host.Run();