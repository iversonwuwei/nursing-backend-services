using Microsoft.EntityFrameworkCore;
using NursingBackend.BuildingBlocks.Context;
using NursingBackend.BuildingBlocks.Contracts;
using NursingBackend.BuildingBlocks.Entities;
using NursingBackend.BuildingBlocks.Hosting;
using NursingBackend.BuildingBlocks.Persistence;
using NursingBackend.Services.Operations;

var builder = WebApplication.CreateBuilder(args);
builder.AddPlatformDefaults();
builder.Services.AddDbContext<OperationsDbContext>(options =>
    options.UseNpgsql(PostgresConnectionStrings.Resolve(builder.Configuration, "OperationsPostgres", "nursing_operations")));

var app = builder.Build();
app.MapPlatformEndpoints(new PlatformServiceDescriptor(
	ServiceName: "operations-service",
	ServiceType: "domain-service",
	BoundedContext: "facility-device-supply-alert",
	Consumers: ["admin-bff", "nani-bff", "notification-service", "ai-orchestration-service"],
	Capabilities: ["facility-management", "equipment-lifecycle", "supply-lifecycle", "alert-case-management"]));

app.MapGet("/api/operations/alerts/summary", async (HttpContext context, OperationsDbContext dbContext, CancellationToken cancellationToken) =>
{
    var alerts = await FilterAlertCases(dbContext.AlertCases.AsNoTracking(), context)
        .ToListAsync(cancellationToken);

    var modules = alerts
        .GroupBy(item => item.Module)
        .Select(group => new AdminAlertModuleSummaryResponse(
            Module: group.Key,
            Pending: group.Count(item => item.Status == "pending"),
            Processing: group.Count(item => item.Status == "processing"),
            Resolved: group.Count(item => item.Status == "resolved"),
            Critical: group.Count(item => item.Level == "critical" && item.Status != "resolved")))
        .OrderBy(item => item.Module)
        .ToList();

    return Results.Ok(new AdminAlertSummaryResponse(modules, DateTimeOffset.UtcNow));
}).RequireAuthorization();

app.MapGet("/api/operations/alerts", async (HttpContext context, OperationsDbContext dbContext, CancellationToken cancellationToken, string? module, string? level, string? status) =>
{
    var alerts = await FilterAlertCases(dbContext.AlertCases.AsNoTracking(), context)
        .Where(item => string.IsNullOrWhiteSpace(module) || string.Equals(item.Module, module, StringComparison.OrdinalIgnoreCase))
        .Where(item => string.IsNullOrWhiteSpace(level) || string.Equals(item.Level, level, StringComparison.OrdinalIgnoreCase))
        .Where(item => string.IsNullOrWhiteSpace(status) || string.Equals(item.Status, status, StringComparison.OrdinalIgnoreCase))
        .OrderBy(item => item.Status == "pending" ? 0 : item.Status == "processing" ? 1 : 2)
        .ThenBy(item => item.Level == "critical" ? 0 : item.Level == "warning" ? 1 : 2)
        .ThenByDescending(item => item.OccurredAtUtc)
        .ToListAsync(cancellationToken);

    var items = alerts.Select(ToResponse).ToList();

    return Results.Ok(items);
}).RequireAuthorization();

app.MapPost("/api/operations/alerts/{alertId}/actions", async (string alertId, AdminAlertActionRequest request, HttpContext context, OperationsDbContext dbContext, TimeProvider timeProvider, CancellationToken cancellationToken) =>
{
    var item = await FilterAlertCases(dbContext.AlertCases, context)
        .FirstOrDefaultAsync(entity => entity.AlertId == alertId, cancellationToken);
    if (item is null)
    {
        return Results.Problem(title: $"告警 {alertId} 不存在。", statusCode: StatusCodes.Status404NotFound);
    }

    var nextStatus = request.Action switch
    {
        "acknowledge" => "processing",
        "dispatch" => "processing",
        "resolve" => "resolved",
        "close" => "resolved",
        _ => item.Status,
    };

    item.Status = nextStatus;
    if (nextStatus == "processing" || nextStatus == "resolved")
    {
        var requestContext = context.GetPlatformRequestContext();
        item.HandledBy = string.IsNullOrWhiteSpace(requestContext?.UserName) ? "当前用户" : requestContext.UserName;
        item.HandledAtUtc = timeProvider.GetUtcNow();
    }

    if (nextStatus == "resolved")
    {
        item.Resolution = request.Note ?? item.Resolution ?? "已完成处置并结案。";
    }

    await dbContext.SaveChangesAsync(cancellationToken);
    return Results.Ok(ToResponse(item));
}).RequireAuthorization();

app.Run();

static IQueryable<AlertCaseEntity> FilterAlertCases(IQueryable<AlertCaseEntity> query, HttpContext context)
{
    var tenantId = context.GetPlatformRequestContext()?.TenantId;
    return string.IsNullOrWhiteSpace(tenantId)
        ? query
        : query.Where(item => item.TenantId == tenantId);
}

static AdminAlertQueueItemResponse ToResponse(AlertCaseEntity item)
{
    return new AdminAlertQueueItemResponse(
        AlertId: item.AlertId,
        Module: item.Module,
        Type: item.Type,
        Level: item.Level,
        Status: item.Status,
        ElderId: item.ElderId,
        ElderlyName: item.ElderlyName,
        RoomNumber: item.RoomNumber,
        Description: item.Description,
        DeviceName: item.DeviceName,
        OccurredAt: FormatTimestamp(item.OccurredAtUtc),
        HandledBy: item.HandledBy,
        HandledAt: item.HandledAtUtc is null ? null : FormatTimestamp(item.HandledAtUtc.Value),
        Resolution: item.Resolution);
}

static string FormatTimestamp(DateTimeOffset value)
{
    return value.ToOffset(TimeSpan.FromHours(8)).ToString("yyyy/MM/dd HH:mm");
}
