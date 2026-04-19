using Microsoft.EntityFrameworkCore;
using System.Text.Json;
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

app.MapGet("/api/operations/activities", async (HttpContext context, OperationsDbContext dbContext, CancellationToken cancellationToken, string? keyword, string? status, string? lifecycleStatus, int page = 1, int pageSize = 20) =>
{
    page = NormalizePage(page);
    pageSize = NormalizePageSize(pageSize);

    var query = FilterActivities(dbContext.Activities.AsNoTracking(), context)
        .Where(item => string.IsNullOrWhiteSpace(keyword)
            || item.Name.Contains(keyword)
            || item.ActivityId.Contains(keyword)
            || item.Category.Contains(keyword)
            || item.Location.Contains(keyword))
        .Where(item => string.IsNullOrWhiteSpace(status) || item.Status == status)
        .Where(item => string.IsNullOrWhiteSpace(lifecycleStatus) || item.LifecycleStatus == lifecycleStatus);

    var total = await query.CountAsync(cancellationToken);
    var items = await query
        .OrderBy(item => item.LifecycleStatus == "待发布" ? 0 : 1)
        .ThenBy(item => item.Status == "进行中" ? 0 : item.Status == "报名中" ? 1 : item.Status == "待发布" ? 2 : 3)
        .ThenBy(item => item.Date)
        .ThenBy(item => item.Time)
        .Skip((page - 1) * pageSize)
        .Take(pageSize)
        .ToListAsync(cancellationToken);

    return Results.Ok(new AdminActivityListResponse(items.Select(ToActivityResponse).ToList(), total, page, pageSize));
}).RequireAuthorization();

app.MapGet("/api/operations/activities/{activityId}", async (string activityId, HttpContext context, OperationsDbContext dbContext, CancellationToken cancellationToken) =>
{
    var item = await FilterActivities(dbContext.Activities.AsNoTracking(), context)
        .FirstOrDefaultAsync(entity => entity.ActivityId == activityId, cancellationToken);
    return item is null
        ? Results.Problem(title: $"活动 {activityId} 不存在。", statusCode: StatusCodes.Status404NotFound)
        : Results.Ok(ToActivityResponse(item));
}).RequireAuthorization();

app.MapPost("/api/operations/activities", async (AdminActivityCreateRequest request, HttpContext context, OperationsDbContext dbContext, TimeProvider timeProvider, CancellationToken cancellationToken) =>
{
    var requestContext = context.GetPlatformRequestContext();
    if (requestContext is null || string.IsNullOrWhiteSpace(requestContext.TenantId))
    {
        return Results.Problem(title: "缺少租户上下文。", statusCode: StatusCodes.Status400BadRequest);
    }

    var now = timeProvider.GetUtcNow();
    var entity = new ActivityEntity
    {
        ActivityId = GenerateOperationsId("ACT"),
        TenantId = requestContext.TenantId,
        Name = request.Name,
        Category = request.Category,
        Date = request.Date,
        Time = request.Time,
        Duration = request.Duration,
        Participants = 0,
        Capacity = request.Capacity,
        Location = request.Location,
        Status = "待发布",
        Teacher = request.Teacher,
        Description = request.Description,
        LifecycleStatus = "待发布",
        CreatedAtUtc = now,
        PublishNote = "已提交活动初稿，等待运营复核发布。",
    };

    await dbContext.Activities.AddAsync(entity, cancellationToken);
    await dbContext.SaveChangesAsync(cancellationToken);
    return Results.Ok(ToActivityResponse(entity));
}).RequireAuthorization();

app.MapPost("/api/operations/activities/{activityId}/actions", async (string activityId, AdminActivityActionRequest request, HttpContext context, OperationsDbContext dbContext, TimeProvider timeProvider, CancellationToken cancellationToken) =>
{
    var entity = await FilterActivities(dbContext.Activities, context)
        .FirstOrDefaultAsync(item => item.ActivityId == activityId, cancellationToken);
    if (entity is null)
    {
        return Results.Problem(title: $"活动 {activityId} 不存在。", statusCode: StatusCodes.Status404NotFound);
    }

    if (request.Action == "publish")
    {
        entity.LifecycleStatus = "已发布";
        if (entity.Status == "待发布")
        {
            entity.Status = "报名中";
        }
        entity.PublishedAtUtc ??= timeProvider.GetUtcNow();
        entity.PublishNote = request.Note ?? entity.PublishNote ?? "已同步到活动报名与执行看板。";
    }

    await dbContext.SaveChangesAsync(cancellationToken);
    return Results.Ok(ToActivityResponse(entity));
}).RequireAuthorization();

app.MapGet("/api/operations/incidents", async (HttpContext context, OperationsDbContext dbContext, CancellationToken cancellationToken, string? keyword, string? level, string? status, int page = 1, int pageSize = 20) =>
{
    page = NormalizePage(page);
    pageSize = NormalizePageSize(pageSize);

    var query = FilterIncidents(dbContext.Incidents.AsNoTracking(), context)
        .Where(item => string.IsNullOrWhiteSpace(keyword)
            || item.Title.Contains(keyword)
            || item.IncidentId.Contains(keyword)
            || item.Room.Contains(keyword)
            || item.Reporter.Contains(keyword)
            || (item.ElderName != null && item.ElderName.Contains(keyword)))
        .Where(item => string.IsNullOrWhiteSpace(level) || item.Level == level)
        .Where(item => string.IsNullOrWhiteSpace(status) || item.Status == status);

    var total = await query.CountAsync(cancellationToken);
    var items = await query
        .OrderBy(item => item.Status == "待分派" ? 0 : item.Status == "处理中" ? 1 : 2)
        .ThenBy(item => item.Level == "严重" ? 0 : item.Level == "一般" ? 1 : 2)
        .ThenByDescending(item => item.OccurredAtUtc)
        .Skip((page - 1) * pageSize)
        .Take(pageSize)
        .ToListAsync(cancellationToken);

    return Results.Ok(new AdminIncidentListResponse(items.Select(ToIncidentResponse).ToList(), total, page, pageSize));
}).RequireAuthorization();

app.MapGet("/api/operations/incidents/{incidentId}", async (string incidentId, HttpContext context, OperationsDbContext dbContext, CancellationToken cancellationToken) =>
{
    var item = await FilterIncidents(dbContext.Incidents.AsNoTracking(), context)
        .FirstOrDefaultAsync(entity => entity.IncidentId == incidentId, cancellationToken);
    return item is null
        ? Results.Problem(title: $"事件 {incidentId} 不存在。", statusCode: StatusCodes.Status404NotFound)
        : Results.Ok(ToIncidentResponse(item));
}).RequireAuthorization();

app.MapPost("/api/operations/incidents", async (AdminIncidentCreateRequest request, HttpContext context, OperationsDbContext dbContext, TimeProvider timeProvider, CancellationToken cancellationToken) =>
{
    var requestContext = context.GetPlatformRequestContext();
    if (requestContext is null || string.IsNullOrWhiteSpace(requestContext.TenantId))
    {
        return Results.Problem(title: "缺少租户上下文。", statusCode: StatusCodes.Status400BadRequest);
    }

    var now = timeProvider.GetUtcNow();
    var entity = new IncidentEntity
    {
        IncidentId = GenerateOperationsId("INC"),
        TenantId = requestContext.TenantId,
        Title = request.Title,
        Level = request.Level,
        ElderName = string.IsNullOrWhiteSpace(request.ElderName) ? null : request.ElderName,
        Room = request.Room,
        Reporter = request.Reporter,
        ReporterRole = request.ReporterRole,
        OccurredAtUtc = ParseDateTime(request.Time, now),
        Status = "待分派",
        Description = request.Description,
        HandlingJson = SerializeStringList(["已记录事故初报，等待值班主管分派。"]),
        NextStep = request.NextStep,
        AttachmentsJson = SerializeStringList(request.Attachments),
        CreatedAtUtc = now,
        StatusNote = "事故初报已提交，等待值班主管确认责任人。",
    };

    await dbContext.Incidents.AddAsync(entity, cancellationToken);
    await dbContext.SaveChangesAsync(cancellationToken);
    return Results.Ok(ToIncidentResponse(entity));
}).RequireAuthorization();

app.MapPost("/api/operations/incidents/{incidentId}/actions", async (string incidentId, AdminIncidentActionRequest request, HttpContext context, OperationsDbContext dbContext, TimeProvider timeProvider, CancellationToken cancellationToken) =>
{
    var entity = await FilterIncidents(dbContext.Incidents, context)
        .FirstOrDefaultAsync(item => item.IncidentId == incidentId, cancellationToken);
    if (entity is null)
    {
        return Results.Problem(title: $"事件 {incidentId} 不存在。", statusCode: StatusCodes.Status404NotFound);
    }

    var handling = DeserializeStringList(entity.HandlingJson).ToList();
    var operatorName = context.GetPlatformRequestContext()?.UserName;
    if (request.Action == "start")
    {
        entity.Status = "处理中";
        entity.AssignedAtUtc ??= timeProvider.GetUtcNow();
        handling.Add(request.Note ?? $"已由{(string.IsNullOrWhiteSpace(operatorName) ? "值班主管" : operatorName)}接手处置。");
        entity.StatusNote = request.Note ?? "已开始处置，等待后续闭环。";
    }
    else if (request.Action == "close")
    {
        entity.Status = "已结案";
        entity.ClosedAtUtc = timeProvider.GetUtcNow();
        handling.Add(request.Note ?? "已完成处置并进入结案复盘。");
        entity.StatusNote = request.Note ?? "已申请结案，等待复盘归档。";
    }

    entity.HandlingJson = SerializeStringList(handling);
    await dbContext.SaveChangesAsync(cancellationToken);
    return Results.Ok(ToIncidentResponse(entity));
}).RequireAuthorization();

app.MapGet("/api/operations/equipment", async (HttpContext context, OperationsDbContext dbContext, CancellationToken cancellationToken, string? keyword, string? category, string? status, string? lifecycleStatus, int page = 1, int pageSize = 20) =>
{
    page = NormalizePage(page);
    pageSize = NormalizePageSize(pageSize);

    var query = FilterEquipment(dbContext.Equipment.AsNoTracking(), context)
        .Where(item => string.IsNullOrWhiteSpace(keyword)
            || item.Name.Contains(keyword)
            || item.EquipmentId.Contains(keyword)
            || item.Location.Contains(keyword)
            || item.Model.Contains(keyword))
        .Where(item => string.IsNullOrWhiteSpace(category) || item.Category == category)
        .Where(item => string.IsNullOrWhiteSpace(status) || item.Status == status)
        .Where(item => string.IsNullOrWhiteSpace(lifecycleStatus) || item.LifecycleStatus == lifecycleStatus);

    var total = await query.CountAsync(cancellationToken);
    var items = await query
        .OrderBy(item => item.LifecycleStatus == "待验收" ? 0 : 1)
        .ThenBy(item => item.Status == "待维修" ? 0 : item.Status == "维修中" ? 1 : item.Status == "正常" ? 2 : 3)
        .ThenBy(item => item.Name)
        .Skip((page - 1) * pageSize)
        .Take(pageSize)
        .ToListAsync(cancellationToken);

    return Results.Ok(new AdminEquipmentListResponse(items.Select(ToEquipmentResponse).ToList(), total, page, pageSize));
}).RequireAuthorization();

app.MapGet("/api/operations/equipment/{equipmentId}", async (string equipmentId, HttpContext context, OperationsDbContext dbContext, CancellationToken cancellationToken) =>
{
    var item = await FilterEquipment(dbContext.Equipment.AsNoTracking(), context)
        .FirstOrDefaultAsync(entity => entity.EquipmentId == equipmentId, cancellationToken);
    return item is null
        ? Results.Problem(title: $"设备 {equipmentId} 不存在。", statusCode: StatusCodes.Status404NotFound)
        : Results.Ok(ToEquipmentResponse(item));
}).RequireAuthorization();

app.MapPost("/api/operations/equipment", async (AdminEquipmentCreateRequest request, HttpContext context, OperationsDbContext dbContext, TimeProvider timeProvider, CancellationToken cancellationToken) =>
{
    var requestContext = context.GetPlatformRequestContext();
    if (requestContext is null || string.IsNullOrWhiteSpace(requestContext.TenantId))
    {
        return Results.Problem(title: "缺少租户上下文。", statusCode: StatusCodes.Status400BadRequest);
    }

    var now = timeProvider.GetUtcNow();
    var entity = new EquipmentEntity
    {
        EquipmentId = GenerateOperationsId("EQ"),
        TenantId = requestContext.TenantId,
        Name = request.Name,
        Category = request.Category,
        Model = request.Model,
        SerialNumber = request.SerialNumber,
        Location = request.Location,
        Status = "正常",
        PurchaseDate = request.PurchaseDate,
        MaintenanceDate = AddMonths(request.PurchaseDate, request.MaintenanceCycle),
        MaintenanceCycle = request.MaintenanceCycle,
        OrganizationId = request.OrganizationId,
        Remarks = request.Remarks,
        Room = request.Location,
        Type = request.Category,
        Signal = 96,
        Battery = 100,
        Uptime = 0,
        MetricsHr = 72,
        MetricsBp = "120/80",
        MetricsTemp = 36.5,
        MetricsSpo2 = 98,
        HistoryJson = SerializeEquipmentHistory([
            new EquipmentHistoryPoint("16:00", 72, 98, "新建设备待验收，初始指标已登记。")
        ]),
        LifecycleStatus = "待验收",
        CreatedAtUtc = now,
        AcceptanceNote = "设备已录入，等待资产管理员验收。",
    };

    await dbContext.Equipment.AddAsync(entity, cancellationToken);
    await dbContext.SaveChangesAsync(cancellationToken);
    return Results.Ok(ToEquipmentResponse(entity));
}).RequireAuthorization();

app.MapPost("/api/operations/equipment/{equipmentId}/activate", async (string equipmentId, AdminEquipmentActivateRequest request, HttpContext context, OperationsDbContext dbContext, TimeProvider timeProvider, CancellationToken cancellationToken) =>
{
    var entity = await FilterEquipment(dbContext.Equipment, context)
        .FirstOrDefaultAsync(item => item.EquipmentId == equipmentId, cancellationToken);
    if (entity is null)
    {
        return Results.Problem(title: $"设备 {equipmentId} 不存在。", statusCode: StatusCodes.Status404NotFound);
    }

    entity.LifecycleStatus = "已入册";
    entity.ActivatedAtUtc ??= timeProvider.GetUtcNow();
    entity.AcceptanceNote = request.AcceptanceNote ?? entity.AcceptanceNote ?? "已完成验收并纳入设备台账。";
    await dbContext.SaveChangesAsync(cancellationToken);
    return Results.Ok(ToEquipmentResponse(entity));
}).RequireAuthorization();

app.MapGet("/api/operations/supplies", async (HttpContext context, OperationsDbContext dbContext, CancellationToken cancellationToken, string? keyword, string? category, string? status, string? lifecycleStatus, int page = 1, int pageSize = 20) =>
{
    page = NormalizePage(page);
    pageSize = NormalizePageSize(pageSize);

    var query = FilterSupplies(dbContext.Supplies.AsNoTracking(), context)
        .Where(item => string.IsNullOrWhiteSpace(keyword)
            || item.Name.Contains(keyword)
            || item.SupplyId.Contains(keyword)
            || item.Supplier.Contains(keyword))
        .Where(item => string.IsNullOrWhiteSpace(category) || item.Category == category)
        .Where(item => string.IsNullOrWhiteSpace(status) || item.Status == status)
        .Where(item => string.IsNullOrWhiteSpace(lifecycleStatus) || item.LifecycleStatus == lifecycleStatus);

    var total = await query.CountAsync(cancellationToken);
    var items = await query
        .OrderBy(item => item.LifecycleStatus == "待上架" ? 0 : 1)
        .ThenBy(item => item.Status == "库存不足" ? 0 : item.Status == "待上架" ? 1 : 2)
        .ThenBy(item => item.Name)
        .Skip((page - 1) * pageSize)
        .Take(pageSize)
        .ToListAsync(cancellationToken);

    return Results.Ok(new AdminSupplyListResponse(items.Select(ToSupplyResponse).ToList(), total, page, pageSize));
}).RequireAuthorization();

app.MapGet("/api/operations/supplies/{supplyId}", async (string supplyId, HttpContext context, OperationsDbContext dbContext, CancellationToken cancellationToken) =>
{
    var item = await FilterSupplies(dbContext.Supplies.AsNoTracking(), context)
        .FirstOrDefaultAsync(entity => entity.SupplyId == supplyId, cancellationToken);
    return item is null
        ? Results.Problem(title: $"物资 {supplyId} 不存在。", statusCode: StatusCodes.Status404NotFound)
        : Results.Ok(ToSupplyResponse(item));
}).RequireAuthorization();

app.MapPost("/api/operations/supplies", async (AdminSupplyIntakeRequest request, HttpContext context, OperationsDbContext dbContext, TimeProvider timeProvider, CancellationToken cancellationToken) =>
{
    var requestContext = context.GetPlatformRequestContext();
    if (requestContext is null || string.IsNullOrWhiteSpace(requestContext.TenantId))
    {
        return Results.Problem(title: "缺少租户上下文。", statusCode: StatusCodes.Status400BadRequest);
    }

    var now = timeProvider.GetUtcNow();
    var intakeDate = FormatDate(now);
    if (!string.IsNullOrWhiteSpace(request.ExistingId))
    {
        var existing = await FilterSupplies(dbContext.Supplies, context)
            .FirstOrDefaultAsync(item => item.SupplyId == request.ExistingId, cancellationToken);
        if (existing is null)
        {
            return Results.Problem(title: $"物资 {request.ExistingId} 不存在。", statusCode: StatusCodes.Status404NotFound);
        }

        var history = DeserializeSupplyHistory(existing.HistoryJson).ToList();
        existing.Stock += request.Quantity;
        existing.LastIntakeQuantity = request.Quantity;
        existing.LastPurchase = intakeDate;
        existing.Status = "待上架";
        existing.LifecycleStatus = "待上架";
        existing.IntakeNote = request.Supplier ?? existing.Supplier;
        if (!string.IsNullOrWhiteSpace(request.Price)) existing.Price = request.Price;
        if (!string.IsNullOrWhiteSpace(request.Supplier)) existing.Supplier = request.Supplier;
        if (!string.IsNullOrWhiteSpace(request.Contact)) existing.Contact = request.Contact;
        history.Insert(0, new SupplyHistoryRecord(intakeDate, request.Quantity, 0, existing.Stock));
        existing.HistoryJson = SerializeSupplyHistory(history);
        await dbContext.SaveChangesAsync(cancellationToken);
        return Results.Ok(ToSupplyResponse(existing));
    }

    var supply = new SupplyEntity
    {
        SupplyId = GenerateOperationsId("SUP"),
        TenantId = requestContext.TenantId,
        Name = request.Name ?? "未命名物资",
        Category = request.Category ?? "未分类",
        Unit = request.Unit ?? "件",
        Stock = request.Quantity,
        MinStock = request.MinStock ?? 0,
        Price = request.Price ?? "¥0",
        Supplier = request.Supplier ?? "待补充供应商",
        Contact = request.Contact ?? "待补充联系方式",
        LastPurchase = intakeDate,
        Status = "待上架",
        LifecycleStatus = "待上架",
        HistoryJson = SerializeSupplyHistory([
            new SupplyHistoryRecord(intakeDate, request.Quantity, 0, request.Quantity)
        ]),
        CreatedAtUtc = now,
        IntakeNote = "已完成入库登记，等待仓储确认上架。",
        LastIntakeQuantity = request.Quantity,
    };

    await dbContext.Supplies.AddAsync(supply, cancellationToken);
    await dbContext.SaveChangesAsync(cancellationToken);
    return Results.Ok(ToSupplyResponse(supply));
}).RequireAuthorization();

app.MapPost("/api/operations/supplies/{supplyId}/activate", async (string supplyId, AdminSupplyActivateRequest request, HttpContext context, OperationsDbContext dbContext, TimeProvider timeProvider, CancellationToken cancellationToken) =>
{
    var entity = await FilterSupplies(dbContext.Supplies, context)
        .FirstOrDefaultAsync(item => item.SupplyId == supplyId, cancellationToken);
    if (entity is null)
    {
        return Results.Problem(title: $"物资 {supplyId} 不存在。", statusCode: StatusCodes.Status404NotFound);
    }

    entity.LifecycleStatus = "已入库";
    entity.ActivatedAtUtc ??= timeProvider.GetUtcNow();
    entity.Status = entity.Stock < entity.MinStock ? "库存不足" : "正常";
    entity.IntakeNote = request.IntakeNote ?? entity.IntakeNote ?? "已确认上架并纳入库存统计。";
    await dbContext.SaveChangesAsync(cancellationToken);
    return Results.Ok(ToSupplyResponse(entity));
}).RequireAuthorization();

app.Run();

static IQueryable<AlertCaseEntity> FilterAlertCases(IQueryable<AlertCaseEntity> query, HttpContext context)
{
    var tenantId = context.GetPlatformRequestContext()?.TenantId;
    return string.IsNullOrWhiteSpace(tenantId)
        ? query
        : query.Where(item => item.TenantId == tenantId);
}

static IQueryable<ActivityEntity> FilterActivities(IQueryable<ActivityEntity> query, HttpContext context)
{
    var tenantId = context.GetPlatformRequestContext()?.TenantId;
    return string.IsNullOrWhiteSpace(tenantId)
        ? query
        : query.Where(item => item.TenantId == tenantId);
}

static IQueryable<IncidentEntity> FilterIncidents(IQueryable<IncidentEntity> query, HttpContext context)
{
    var tenantId = context.GetPlatformRequestContext()?.TenantId;
    return string.IsNullOrWhiteSpace(tenantId)
        ? query
        : query.Where(item => item.TenantId == tenantId);
}

static IQueryable<EquipmentEntity> FilterEquipment(IQueryable<EquipmentEntity> query, HttpContext context)
{
    var tenantId = context.GetPlatformRequestContext()?.TenantId;
    return string.IsNullOrWhiteSpace(tenantId)
        ? query
        : query.Where(item => item.TenantId == tenantId);
}

static IQueryable<SupplyEntity> FilterSupplies(IQueryable<SupplyEntity> query, HttpContext context)
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

static AdminActivityRecordResponse ToActivityResponse(ActivityEntity item)
{
    return new AdminActivityRecordResponse(
        ActivityId: item.ActivityId,
        TenantId: item.TenantId,
        Name: item.Name,
        Category: item.Category,
        Date: item.Date,
        Time: item.Time,
        Duration: item.Duration,
        Participants: item.Participants,
        Capacity: item.Capacity,
        Location: item.Location,
        Status: item.Status,
        Teacher: item.Teacher,
        Description: item.Description,
        LifecycleStatus: item.LifecycleStatus,
        CreatedAt: FormatDateTime(item.CreatedAtUtc),
        PublishedAt: item.PublishedAtUtc is null ? null : FormatDateTime(item.PublishedAtUtc.Value),
        PublishNote: item.PublishNote);
}

static AdminIncidentRecordResponse ToIncidentResponse(IncidentEntity item)
{
    return new AdminIncidentRecordResponse(
        IncidentId: item.IncidentId,
        TenantId: item.TenantId,
        Title: item.Title,
        Level: item.Level,
        ElderName: item.ElderName,
        Room: item.Room,
        Reporter: item.Reporter,
        ReporterRole: item.ReporterRole,
        Time: FormatDateTime(item.OccurredAtUtc),
        Status: item.Status,
        Description: item.Description,
        Handling: DeserializeStringList(item.HandlingJson),
        NextStep: item.NextStep,
        Attachments: DeserializeStringList(item.AttachmentsJson),
        CreatedAt: FormatDateTime(item.CreatedAtUtc),
        AssignedAt: item.AssignedAtUtc is null ? null : FormatDateTime(item.AssignedAtUtc.Value),
        ClosedAt: item.ClosedAtUtc is null ? null : FormatDateTime(item.ClosedAtUtc.Value),
        StatusNote: item.StatusNote);
}

static AdminEquipmentRecordResponse ToEquipmentResponse(EquipmentEntity item)
{
    var history = DeserializeEquipmentHistory(item.HistoryJson)
        .Select(point => new AdminEquipmentHistoryPointResponse(point.Time, point.Hr, point.Spo2, point.Note))
        .ToList();

    return new AdminEquipmentRecordResponse(
        EquipmentId: item.EquipmentId,
        TenantId: item.TenantId,
        Name: item.Name,
        Category: item.Category,
        Model: item.Model,
        SerialNumber: item.SerialNumber,
        Location: item.Location,
        Status: item.Status,
        PurchaseDate: item.PurchaseDate,
        MaintenanceDate: item.MaintenanceDate,
        MaintenanceCycle: item.MaintenanceCycle,
        OrganizationId: item.OrganizationId,
        Remarks: item.Remarks,
        Room: item.Room,
        Type: item.Type,
        Signal: item.Signal,
        Battery: item.Battery,
        Uptime: item.Uptime,
        Metrics: new AdminEquipmentMetricSnapshotResponse(item.MetricsHr, item.MetricsBp, item.MetricsTemp, item.MetricsSpo2),
        History: history,
        LifecycleStatus: item.LifecycleStatus,
        CreatedAt: FormatDateTime(item.CreatedAtUtc),
        ActivatedAt: item.ActivatedAtUtc is null ? null : FormatDateTime(item.ActivatedAtUtc.Value),
        AcceptanceNote: item.AcceptanceNote);
}

static AdminSupplyRecordResponse ToSupplyResponse(SupplyEntity item)
{
    var history = DeserializeSupplyHistory(item.HistoryJson)
        .Select(point => new AdminSupplyHistoryRecordResponse(point.Date, point.In, point.Out, point.Balance))
        .ToList();

    return new AdminSupplyRecordResponse(
        SupplyId: item.SupplyId,
        TenantId: item.TenantId,
        Name: item.Name,
        Category: item.Category,
        Unit: item.Unit,
        Stock: item.Stock,
        MinStock: item.MinStock,
        Price: item.Price,
        Supplier: item.Supplier,
        Contact: item.Contact,
        LastPurchase: item.LastPurchase,
        Status: item.Status,
        LifecycleStatus: item.LifecycleStatus,
        History: history,
        CreatedAt: FormatDateTime(item.CreatedAtUtc),
        ActivatedAt: item.ActivatedAtUtc is null ? null : FormatDateTime(item.ActivatedAtUtc.Value),
        IntakeNote: item.IntakeNote,
        LastIntakeQuantity: item.LastIntakeQuantity);
}

static string FormatTimestamp(DateTimeOffset value)
{
    return value.ToOffset(TimeSpan.FromHours(8)).ToString("yyyy/MM/dd HH:mm");
}

static string FormatDateTime(DateTimeOffset value)
{
    return value.ToOffset(TimeSpan.FromHours(8)).ToString("yyyy-MM-dd HH:mm");
}

static string FormatDate(DateTimeOffset value)
{
    return value.ToOffset(TimeSpan.FromHours(8)).ToString("yyyy-MM-dd");
}

static int NormalizePage(int page) => page <= 0 ? 1 : page;

static int NormalizePageSize(int pageSize) => Math.Clamp(pageSize <= 0 ? 20 : pageSize, 1, 100);

static string GenerateOperationsId(string prefix)
    => $"{prefix}-{Guid.NewGuid().ToString("N")[..8].ToUpperInvariant()}";

static DateTimeOffset ParseDateTime(string value, DateTimeOffset fallback)
    => DateTimeOffset.TryParse(value, out var parsed) ? parsed : fallback;

static string AddMonths(string date, int months)
{
    if (!DateTime.TryParse(date, out var parsed))
    {
        return date;
    }

    return parsed.AddMonths(months).ToString("yyyy-MM-dd");
}

static IReadOnlyList<string> DeserializeStringList(string json)
    => JsonSerializer.Deserialize<List<string>>(json) ?? [];

static string SerializeStringList(IReadOnlyList<string> items)
    => JsonSerializer.Serialize(items.Where(item => !string.IsNullOrWhiteSpace(item)).ToList());

static IReadOnlyList<EquipmentHistoryPoint> DeserializeEquipmentHistory(string json)
    => JsonSerializer.Deserialize<List<EquipmentHistoryPoint>>(json) ?? [];

static string SerializeEquipmentHistory(IReadOnlyList<EquipmentHistoryPoint> items)
    => JsonSerializer.Serialize(items);

static IReadOnlyList<SupplyHistoryRecord> DeserializeSupplyHistory(string json)
    => JsonSerializer.Deserialize<List<SupplyHistoryRecord>>(json) ?? [];

static string SerializeSupplyHistory(IReadOnlyList<SupplyHistoryRecord> items)
    => JsonSerializer.Serialize(items);

file sealed record EquipmentHistoryPoint(string Time, int Hr, int Spo2, string Note);

file sealed record SupplyHistoryRecord(string Date, int In, int Out, int Balance);
