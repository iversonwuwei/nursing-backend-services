using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using NursingBackend.BuildingBlocks.Contracts;
using NursingBackend.BuildingBlocks.Context;
using NursingBackend.BuildingBlocks.Entities;
using NursingBackend.BuildingBlocks.Hosting;
using NursingBackend.Services.Config;

var builder = WebApplication.CreateBuilder(args);
builder.AddPlatformDefaults();
builder.Services.AddDbContext<ConfigDbContext>(options =>
    options.UseNpgsql(
        builder.Configuration.GetConnectionString("Postgres")
        ?? "Host=localhost;Port=5432;Database=nursing_platform;Username=nursing;Password=nursing"));

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var dbContext = scope.ServiceProvider.GetRequiredService<ConfigDbContext>();
    await dbContext.Database.EnsureCreatedAsync();
    await ConfigDatabaseBootstrapper.EnsureSchemaAsync(dbContext);
    if (app.Environment.IsDevelopment())
    {
        await ConfigSeedData.SeedAsync(dbContext);
    }
}

app.MapPlatformEndpoints(new PlatformServiceDescriptor(
    ServiceName: "config-service",
    ServiceType: "domain-service",
    BoundedContext: "content-management",
    Consumers: ["admin-bff", "family-bff", "nani-bff"],
    Capabilities: ["static-text-management", "option-group-management", "audit-log", "app-config-snapshot"]));

// ── Static Text Endpoints ────────────────────────────────────

app.MapGet("/api/config/static-texts", async (
    HttpContext context,
    ConfigDbContext db,
    string? ns,
    string? locale,
    string? keyword,
    int page = 1,
    int pageSize = 20) =>
{
    var requestContext = context.GetPlatformRequestContext();
    if (requestContext is null || string.IsNullOrWhiteSpace(requestContext.TenantId))
        return Results.Problem(title: "缺少租户上下文。", statusCode: StatusCodes.Status400BadRequest);

    var query = db.StaticTexts
        .Where(x => x.TenantId == requestContext.TenantId);

    if (!string.IsNullOrWhiteSpace(ns))
        query = query.Where(x => x.Namespace == ns);

    if (!string.IsNullOrWhiteSpace(locale))
        query = query.Where(x => x.Locale == locale);

    if (!string.IsNullOrWhiteSpace(keyword))
        query = query.Where(x => x.TextKey.Contains(keyword) || x.TextValue.Contains(keyword));

    var total = await query.CountAsync();
    var items = await query
        .OrderBy(x => x.Namespace).ThenBy(x => x.TextKey)
        .Skip((page - 1) * pageSize)
        .Take(pageSize)
        .Select(x => new StaticTextResponse(
            x.StaticTextId, x.TenantId, x.Namespace, x.TextKey, x.Locale,
            x.TextValue, x.Description, x.Version, x.UpdatedBy,
            x.CreatedAtUtc, x.UpdatedAtUtc))
        .ToListAsync();

    return Results.Ok(new StaticTextListResponse(items, total, page, pageSize));
}).RequireAuthorization();

app.MapGet("/api/config/static-texts/{id}", async (
    string id,
    HttpContext context,
    ConfigDbContext db) =>
{
    var requestContext = context.GetPlatformRequestContext();
    if (requestContext is null || string.IsNullOrWhiteSpace(requestContext.TenantId))
        return Results.Problem(title: "缺少租户上下文。", statusCode: StatusCodes.Status400BadRequest);

    var entity = await db.StaticTexts.FirstOrDefaultAsync(
        x => x.StaticTextId == id && x.TenantId == requestContext.TenantId);

    if (entity is null)
        return Results.Problem(title: "文本条目不存在。", statusCode: StatusCodes.Status404NotFound);

    return Results.Ok(new StaticTextResponse(
        entity.StaticTextId, entity.TenantId, entity.Namespace, entity.TextKey, entity.Locale,
        entity.TextValue, entity.Description, entity.Version, entity.UpdatedBy,
        entity.CreatedAtUtc, entity.UpdatedAtUtc));
}).RequireAuthorization();

app.MapPost("/api/config/static-texts", async (
    StaticTextCreateRequest request,
    HttpContext context,
    ConfigDbContext db) =>
{
    var requestContext = context.GetPlatformRequestContext();
    if (requestContext is null || string.IsNullOrWhiteSpace(requestContext.TenantId))
        return Results.Problem(title: "缺少租户上下文。", statusCode: StatusCodes.Status400BadRequest);

    var now = DateTimeOffset.UtcNow;
    var entity = new StaticTextEntity
    {
        StaticTextId = $"ST-{now.ToUnixTimeMilliseconds()}",
        TenantId = requestContext.TenantId,
        Namespace = request.Namespace,
        TextKey = request.TextKey,
        Locale = request.Locale,
        TextValue = request.TextValue,
        Description = request.Description,
        Version = 1,
        UpdatedBy = requestContext.UserId,
        CreatedAtUtc = now,
        UpdatedAtUtc = now,
    };

    db.StaticTexts.Add(entity);

    db.AuditLogs.Add(new ContentAuditLogEntity
    {
        AuditLogId = $"AL-{now.ToUnixTimeMilliseconds()}",
        TenantId = requestContext.TenantId,
        OperatorId = requestContext.UserId,
        OperatorName = requestContext.UserName,
        ResourceType = "static_text",
        ResourceId = entity.StaticTextId,
        Action = "create",
        AfterSnapshotJson = JsonSerializer.Serialize(request, JsonSerializerOptions.Web),
        CreatedAtUtc = now,
    });

    await db.SaveChangesAsync();

    return Results.Ok(new StaticTextResponse(
        entity.StaticTextId, entity.TenantId, entity.Namespace, entity.TextKey, entity.Locale,
        entity.TextValue, entity.Description, entity.Version, entity.UpdatedBy,
        entity.CreatedAtUtc, entity.UpdatedAtUtc));
}).RequireAuthorization();

app.MapPut("/api/config/static-texts/{id}", async (
    string id,
    StaticTextUpdateRequest request,
    HttpContext context,
    ConfigDbContext db) =>
{
    var requestContext = context.GetPlatformRequestContext();
    if (requestContext is null || string.IsNullOrWhiteSpace(requestContext.TenantId))
        return Results.Problem(title: "缺少租户上下文。", statusCode: StatusCodes.Status400BadRequest);

    var entity = await db.StaticTexts.FirstOrDefaultAsync(
        x => x.StaticTextId == id && x.TenantId == requestContext.TenantId);

    if (entity is null)
        return Results.Problem(title: "文本条目不存在。", statusCode: StatusCodes.Status404NotFound);

    if (entity.Version != request.Version)
        return Results.Problem(title: "该文本已被其他管理员修改，请刷新后重试。", statusCode: StatusCodes.Status409Conflict);

    var beforeJson = JsonSerializer.Serialize(new { entity.TextValue, entity.Description }, JsonSerializerOptions.Web);

    entity.TextValue = request.TextValue;
    entity.Description = request.Description;
    entity.Version++;
    entity.UpdatedBy = requestContext.UserId;
    entity.UpdatedAtUtc = DateTimeOffset.UtcNow;

    db.AuditLogs.Add(new ContentAuditLogEntity
    {
        AuditLogId = $"AL-{DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}",
        TenantId = requestContext.TenantId,
        OperatorId = requestContext.UserId,
        OperatorName = requestContext.UserName,
        ResourceType = "static_text",
        ResourceId = entity.StaticTextId,
        Action = "update",
        BeforeSnapshotJson = beforeJson,
        AfterSnapshotJson = JsonSerializer.Serialize(new { request.TextValue, request.Description }, JsonSerializerOptions.Web),
        CreatedAtUtc = DateTimeOffset.UtcNow,
    });

    await db.SaveChangesAsync();

    return Results.Ok(new StaticTextResponse(
        entity.StaticTextId, entity.TenantId, entity.Namespace, entity.TextKey, entity.Locale,
        entity.TextValue, entity.Description, entity.Version, entity.UpdatedBy,
        entity.CreatedAtUtc, entity.UpdatedAtUtc));
}).RequireAuthorization();

app.MapDelete("/api/config/static-texts/{id}", async (
    string id,
    HttpContext context,
    ConfigDbContext db) =>
{
    var requestContext = context.GetPlatformRequestContext();
    if (requestContext is null || string.IsNullOrWhiteSpace(requestContext.TenantId))
        return Results.Problem(title: "缺少租户上下文。", statusCode: StatusCodes.Status400BadRequest);

    var entity = await db.StaticTexts.FirstOrDefaultAsync(
        x => x.StaticTextId == id && x.TenantId == requestContext.TenantId);

    if (entity is null)
        return Results.Problem(title: "文本条目不存在。", statusCode: StatusCodes.Status404NotFound);

    db.StaticTexts.Remove(entity);

    db.AuditLogs.Add(new ContentAuditLogEntity
    {
        AuditLogId = $"AL-{DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}",
        TenantId = requestContext.TenantId,
        OperatorId = requestContext.UserId,
        OperatorName = requestContext.UserName,
        ResourceType = "static_text",
        ResourceId = entity.StaticTextId,
        Action = "delete",
        BeforeSnapshotJson = JsonSerializer.Serialize(
            new { entity.Namespace, entity.TextKey, entity.Locale, entity.TextValue },
            JsonSerializerOptions.Web),
        CreatedAtUtc = DateTimeOffset.UtcNow,
    });

    await db.SaveChangesAsync();
    return Results.Ok(new { success = true });
}).RequireAuthorization();

app.MapGet("/api/config/static-texts/namespaces", async (
    HttpContext context,
    ConfigDbContext db) =>
{
    var requestContext = context.GetPlatformRequestContext();
    if (requestContext is null || string.IsNullOrWhiteSpace(requestContext.TenantId))
        return Results.Problem(title: "缺少租户上下文。", statusCode: StatusCodes.Status400BadRequest);

    var namespaces = await db.StaticTexts
        .Where(x => x.TenantId == requestContext.TenantId)
        .Select(x => x.Namespace)
        .Distinct()
        .OrderBy(x => x)
        .ToListAsync();

    return Results.Ok(namespaces);
}).RequireAuthorization();

// ── Option Group Endpoints ───────────────────────────────────

app.MapGet("/api/config/option-groups", async (
    HttpContext context,
    ConfigDbContext db,
    string? status,
    string? keyword) =>
{
    var requestContext = context.GetPlatformRequestContext();
    if (requestContext is null || string.IsNullOrWhiteSpace(requestContext.TenantId))
        return Results.Problem(title: "缺少租户上下文。", statusCode: StatusCodes.Status400BadRequest);

    var query = db.OptionGroups
        .Where(x => x.TenantId == requestContext.TenantId);

    if (!string.IsNullOrWhiteSpace(status))
        query = query.Where(x => x.Status == status);

    if (!string.IsNullOrWhiteSpace(keyword))
        query = query.Where(x => x.GroupCode.Contains(keyword) || x.GroupName.Contains(keyword));

    var groups = await query.OrderBy(x => x.GroupCode).ToListAsync();

    var groupIds = groups.Select(g => g.OptionGroupId).ToList();
    var itemCounts = await db.OptionItems
        .Where(x => groupIds.Contains(x.GroupId))
        .GroupBy(x => x.GroupId)
        .Select(g => new { GroupId = g.Key, Count = g.Count() })
        .ToDictionaryAsync(x => x.GroupId, x => x.Count);

    var items = groups.Select(g => new OptionGroupResponse(
        g.OptionGroupId, g.TenantId, g.GroupCode, g.GroupName, g.Description,
        g.IsSystem, g.Status,
        itemCounts.GetValueOrDefault(g.OptionGroupId, 0),
        g.UpdatedAtUtc)).ToList();

    return Results.Ok(new OptionGroupListResponse(items));
}).RequireAuthorization();

app.MapPost("/api/config/option-groups", async (
    OptionGroupCreateRequest request,
    HttpContext context,
    ConfigDbContext db) =>
{
    var requestContext = context.GetPlatformRequestContext();
    if (requestContext is null || string.IsNullOrWhiteSpace(requestContext.TenantId))
        return Results.Problem(title: "缺少租户上下文。", statusCode: StatusCodes.Status400BadRequest);

    var now = DateTimeOffset.UtcNow;
    var entity = new OptionGroupEntity
    {
        OptionGroupId = $"OG-{now.ToUnixTimeMilliseconds()}",
        TenantId = requestContext.TenantId,
        GroupCode = request.GroupCode,
        GroupName = request.GroupName,
        Description = request.Description,
        IsSystem = false,
        Status = "active",
        CreatedAtUtc = now,
        UpdatedAtUtc = now,
    };

    db.OptionGroups.Add(entity);

    db.AuditLogs.Add(new ContentAuditLogEntity
    {
        AuditLogId = $"AL-{now.ToUnixTimeMilliseconds()}",
        TenantId = requestContext.TenantId,
        OperatorId = requestContext.UserId,
        OperatorName = requestContext.UserName,
        ResourceType = "option_group",
        ResourceId = entity.OptionGroupId,
        Action = "create",
        AfterSnapshotJson = JsonSerializer.Serialize(request, JsonSerializerOptions.Web),
        CreatedAtUtc = now,
    });

    await db.SaveChangesAsync();

    return Results.Ok(new OptionGroupResponse(
        entity.OptionGroupId, entity.TenantId, entity.GroupCode, entity.GroupName,
        entity.Description, entity.IsSystem, entity.Status, 0, entity.UpdatedAtUtc));
}).RequireAuthorization();

app.MapPut("/api/config/option-groups/{id}", async (
    string id,
    OptionGroupUpdateRequest request,
    HttpContext context,
    ConfigDbContext db) =>
{
    var requestContext = context.GetPlatformRequestContext();
    if (requestContext is null || string.IsNullOrWhiteSpace(requestContext.TenantId))
        return Results.Problem(title: "缺少租户上下文。", statusCode: StatusCodes.Status400BadRequest);

    var entity = await db.OptionGroups.FirstOrDefaultAsync(
        x => x.OptionGroupId == id && x.TenantId == requestContext.TenantId);

    if (entity is null)
        return Results.Problem(title: "选项分组不存在。", statusCode: StatusCodes.Status404NotFound);

    var beforeJson = JsonSerializer.Serialize(
        new { entity.GroupName, entity.Description }, JsonSerializerOptions.Web);

    entity.GroupName = request.GroupName;
    entity.Description = request.Description;
    entity.UpdatedAtUtc = DateTimeOffset.UtcNow;

    db.AuditLogs.Add(new ContentAuditLogEntity
    {
        AuditLogId = $"AL-{DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}",
        TenantId = requestContext.TenantId,
        OperatorId = requestContext.UserId,
        OperatorName = requestContext.UserName,
        ResourceType = "option_group",
        ResourceId = entity.OptionGroupId,
        Action = "update",
        BeforeSnapshotJson = beforeJson,
        AfterSnapshotJson = JsonSerializer.Serialize(request, JsonSerializerOptions.Web),
        CreatedAtUtc = DateTimeOffset.UtcNow,
    });

    await db.SaveChangesAsync();

    var itemCount = await db.OptionItems.CountAsync(x => x.GroupId == id);
    return Results.Ok(new OptionGroupResponse(
        entity.OptionGroupId, entity.TenantId, entity.GroupCode, entity.GroupName,
        entity.Description, entity.IsSystem, entity.Status, itemCount, entity.UpdatedAtUtc));
}).RequireAuthorization();

app.MapDelete("/api/config/option-groups/{id}", async (
    string id,
    HttpContext context,
    ConfigDbContext db) =>
{
    var requestContext = context.GetPlatformRequestContext();
    if (requestContext is null || string.IsNullOrWhiteSpace(requestContext.TenantId))
        return Results.Problem(title: "缺少租户上下文。", statusCode: StatusCodes.Status400BadRequest);

    var entity = await db.OptionGroups.FirstOrDefaultAsync(
        x => x.OptionGroupId == id && x.TenantId == requestContext.TenantId);

    if (entity is null)
        return Results.Problem(title: "选项分组不存在。", statusCode: StatusCodes.Status404NotFound);

    if (entity.IsSystem)
        return Results.Problem(title: "系统内置分组不可删除。", statusCode: StatusCodes.Status400BadRequest);

    var hasItems = await db.OptionItems.AnyAsync(x => x.GroupId == id);
    if (hasItems)
        return Results.Problem(title: "分组下存在选项，请先删除所有选项。", statusCode: StatusCodes.Status400BadRequest);

    db.OptionGroups.Remove(entity);

    db.AuditLogs.Add(new ContentAuditLogEntity
    {
        AuditLogId = $"AL-{DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}",
        TenantId = requestContext.TenantId,
        OperatorId = requestContext.UserId,
        OperatorName = requestContext.UserName,
        ResourceType = "option_group",
        ResourceId = entity.OptionGroupId,
        Action = "delete",
        BeforeSnapshotJson = JsonSerializer.Serialize(
            new { entity.GroupCode, entity.GroupName }, JsonSerializerOptions.Web),
        CreatedAtUtc = DateTimeOffset.UtcNow,
    });

    await db.SaveChangesAsync();
    return Results.Ok(new { success = true });
}).RequireAuthorization();

// ── Option Item Endpoints ────────────────────────────────────

app.MapGet("/api/config/option-groups/{groupId}/items", async (
    string groupId,
    HttpContext context,
    ConfigDbContext db) =>
{
    var requestContext = context.GetPlatformRequestContext();
    if (requestContext is null || string.IsNullOrWhiteSpace(requestContext.TenantId))
        return Results.Problem(title: "缺少租户上下文。", statusCode: StatusCodes.Status400BadRequest);

    var groupExists = await db.OptionGroups.AnyAsync(
        x => x.OptionGroupId == groupId && x.TenantId == requestContext.TenantId);
    if (!groupExists)
        return Results.Problem(title: "选项分组不存在。", statusCode: StatusCodes.Status404NotFound);

    var items = await db.OptionItems
        .Where(x => x.GroupId == groupId)
        .OrderBy(x => x.SortOrder)
        .Select(x => new OptionItemResponse(
            x.OptionItemId, x.GroupId, x.OptionCode, x.LabelZh, x.LabelEn,
            x.SortOrder, x.IsActive, x.IsDefault, x.UpdatedAtUtc))
        .ToListAsync();

    return Results.Ok(items);
}).RequireAuthorization();

app.MapPost("/api/config/option-groups/{groupId}/items", async (
    string groupId,
    OptionItemCreateRequest request,
    HttpContext context,
    ConfigDbContext db) =>
{
    var requestContext = context.GetPlatformRequestContext();
    if (requestContext is null || string.IsNullOrWhiteSpace(requestContext.TenantId))
        return Results.Problem(title: "缺少租户上下文。", statusCode: StatusCodes.Status400BadRequest);

    var groupExists = await db.OptionGroups.AnyAsync(
        x => x.OptionGroupId == groupId && x.TenantId == requestContext.TenantId);
    if (!groupExists)
        return Results.Problem(title: "选项分组不存在。", statusCode: StatusCodes.Status404NotFound);

    var now = DateTimeOffset.UtcNow;
    var entity = new OptionItemEntity
    {
        OptionItemId = $"OI-{now.ToUnixTimeMilliseconds()}",
        GroupId = groupId,
        OptionCode = request.OptionCode,
        LabelZh = request.LabelZh,
        LabelEn = request.LabelEn,
        SortOrder = request.SortOrder,
        IsActive = true,
        IsDefault = request.IsDefault,
        CreatedAtUtc = now,
        UpdatedAtUtc = now,
    };

    db.OptionItems.Add(entity);

    db.AuditLogs.Add(new ContentAuditLogEntity
    {
        AuditLogId = $"AL-{now.ToUnixTimeMilliseconds()}",
        TenantId = requestContext.TenantId,
        OperatorId = requestContext.UserId,
        OperatorName = requestContext.UserName,
        ResourceType = "option_item",
        ResourceId = entity.OptionItemId,
        Action = "create",
        AfterSnapshotJson = JsonSerializer.Serialize(request, JsonSerializerOptions.Web),
        CreatedAtUtc = now,
    });

    await db.SaveChangesAsync();

    return Results.Ok(new OptionItemResponse(
        entity.OptionItemId, entity.GroupId, entity.OptionCode, entity.LabelZh, entity.LabelEn,
        entity.SortOrder, entity.IsActive, entity.IsDefault, entity.UpdatedAtUtc));
}).RequireAuthorization();

app.MapPut("/api/config/option-groups/{groupId}/items/{itemId}", async (
    string groupId,
    string itemId,
    OptionItemUpdateRequest request,
    HttpContext context,
    ConfigDbContext db) =>
{
    var requestContext = context.GetPlatformRequestContext();
    if (requestContext is null || string.IsNullOrWhiteSpace(requestContext.TenantId))
        return Results.Problem(title: "缺少租户上下文。", statusCode: StatusCodes.Status400BadRequest);

    var entity = await db.OptionItems.FirstOrDefaultAsync(
        x => x.OptionItemId == itemId && x.GroupId == groupId);

    if (entity is null)
        return Results.Problem(title: "选项不存在。", statusCode: StatusCodes.Status404NotFound);

    var beforeJson = JsonSerializer.Serialize(
        new { entity.LabelZh, entity.LabelEn, entity.SortOrder, entity.IsDefault }, JsonSerializerOptions.Web);

    entity.LabelZh = request.LabelZh;
    entity.LabelEn = request.LabelEn;
    entity.SortOrder = request.SortOrder;
    entity.IsDefault = request.IsDefault;
    entity.UpdatedAtUtc = DateTimeOffset.UtcNow;

    db.AuditLogs.Add(new ContentAuditLogEntity
    {
        AuditLogId = $"AL-{DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}",
        TenantId = requestContext.TenantId,
        OperatorId = requestContext.UserId,
        OperatorName = requestContext.UserName,
        ResourceType = "option_item",
        ResourceId = entity.OptionItemId,
        Action = "update",
        BeforeSnapshotJson = beforeJson,
        AfterSnapshotJson = JsonSerializer.Serialize(request, JsonSerializerOptions.Web),
        CreatedAtUtc = DateTimeOffset.UtcNow,
    });

    await db.SaveChangesAsync();

    return Results.Ok(new OptionItemResponse(
        entity.OptionItemId, entity.GroupId, entity.OptionCode, entity.LabelZh, entity.LabelEn,
        entity.SortOrder, entity.IsActive, entity.IsDefault, entity.UpdatedAtUtc));
}).RequireAuthorization();

app.MapDelete("/api/config/option-groups/{groupId}/items/{itemId}", async (
    string groupId,
    string itemId,
    HttpContext context,
    ConfigDbContext db) =>
{
    var requestContext = context.GetPlatformRequestContext();
    if (requestContext is null || string.IsNullOrWhiteSpace(requestContext.TenantId))
        return Results.Problem(title: "缺少租户上下文。", statusCode: StatusCodes.Status400BadRequest);

    var entity = await db.OptionItems.FirstOrDefaultAsync(
        x => x.OptionItemId == itemId && x.GroupId == groupId);

    if (entity is null)
        return Results.Problem(title: "选项不存在。", statusCode: StatusCodes.Status404NotFound);

    db.OptionItems.Remove(entity);

    db.AuditLogs.Add(new ContentAuditLogEntity
    {
        AuditLogId = $"AL-{DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}",
        TenantId = requestContext.TenantId,
        OperatorId = requestContext.UserId,
        OperatorName = requestContext.UserName,
        ResourceType = "option_item",
        ResourceId = entity.OptionItemId,
        Action = "delete",
        BeforeSnapshotJson = JsonSerializer.Serialize(
            new { entity.OptionCode, entity.LabelZh }, JsonSerializerOptions.Web),
        CreatedAtUtc = DateTimeOffset.UtcNow,
    });

    await db.SaveChangesAsync();
    return Results.Ok(new { success = true });
}).RequireAuthorization();

app.MapPatch("/api/config/option-groups/{groupId}/items/{itemId}/toggle", async (
    string groupId,
    string itemId,
    HttpContext context,
    ConfigDbContext db) =>
{
    var requestContext = context.GetPlatformRequestContext();
    if (requestContext is null || string.IsNullOrWhiteSpace(requestContext.TenantId))
        return Results.Problem(title: "缺少租户上下文。", statusCode: StatusCodes.Status400BadRequest);

    var entity = await db.OptionItems.FirstOrDefaultAsync(
        x => x.OptionItemId == itemId && x.GroupId == groupId);

    if (entity is null)
        return Results.Problem(title: "选项不存在。", statusCode: StatusCodes.Status404NotFound);

    entity.IsActive = !entity.IsActive;
    entity.UpdatedAtUtc = DateTimeOffset.UtcNow;

    db.AuditLogs.Add(new ContentAuditLogEntity
    {
        AuditLogId = $"AL-{DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}",
        TenantId = requestContext.TenantId,
        OperatorId = requestContext.UserId,
        OperatorName = requestContext.UserName,
        ResourceType = "option_item",
        ResourceId = entity.OptionItemId,
        Action = entity.IsActive ? "enable" : "disable",
        CreatedAtUtc = DateTimeOffset.UtcNow,
    });

    await db.SaveChangesAsync();

    return Results.Ok(new OptionItemResponse(
        entity.OptionItemId, entity.GroupId, entity.OptionCode, entity.LabelZh, entity.LabelEn,
        entity.SortOrder, entity.IsActive, entity.IsDefault, entity.UpdatedAtUtc));
}).RequireAuthorization();

app.MapPut("/api/config/option-groups/{groupId}/items/reorder", async (
    string groupId,
    OptionItemReorderRequest request,
    HttpContext context,
    ConfigDbContext db) =>
{
    var requestContext = context.GetPlatformRequestContext();
    if (requestContext is null || string.IsNullOrWhiteSpace(requestContext.TenantId))
        return Results.Problem(title: "缺少租户上下文。", statusCode: StatusCodes.Status400BadRequest);

    var items = await db.OptionItems
        .Where(x => x.GroupId == groupId)
        .ToListAsync();

    foreach (var order in request.Ordering)
    {
        var item = items.FirstOrDefault(x => x.OptionItemId == order.ItemId);
        if (item is not null)
        {
            item.SortOrder = order.SortOrder;
            item.UpdatedAtUtc = DateTimeOffset.UtcNow;
        }
    }

    await db.SaveChangesAsync();
    return Results.Ok(new { success = true });
}).RequireAuthorization();

// ── Audit Log Endpoints ──────────────────────────────────────

app.MapGet("/api/config/audit-logs", async (
    HttpContext context,
    ConfigDbContext db,
    string? resourceType,
    string? operatorId,
    int page = 1,
    int pageSize = 20) =>
{
    var requestContext = context.GetPlatformRequestContext();
    if (requestContext is null || string.IsNullOrWhiteSpace(requestContext.TenantId))
        return Results.Problem(title: "缺少租户上下文。", statusCode: StatusCodes.Status400BadRequest);

    var query = db.AuditLogs
        .Where(x => x.TenantId == requestContext.TenantId);

    if (!string.IsNullOrWhiteSpace(resourceType))
        query = query.Where(x => x.ResourceType == resourceType);

    if (!string.IsNullOrWhiteSpace(operatorId))
        query = query.Where(x => x.OperatorId == operatorId);

    var total = await query.CountAsync();
    var items = await query
        .OrderByDescending(x => x.CreatedAtUtc)
        .Skip((page - 1) * pageSize)
        .Take(pageSize)
        .Select(x => new ContentAuditLogResponse(
            x.AuditLogId, x.TenantId, x.OperatorId, x.OperatorName,
            x.ResourceType, x.ResourceId, x.Action,
            x.BeforeSnapshotJson, x.AfterSnapshotJson, x.CreatedAtUtc))
        .ToListAsync();

    return Results.Ok(new ContentAuditLogListResponse(items, total, page, pageSize));
}).RequireAuthorization();

app.MapGet("/api/config/audit-logs/{resourceType}/{resourceId}", async (
    string resourceType,
    string resourceId,
    HttpContext context,
    ConfigDbContext db) =>
{
    var requestContext = context.GetPlatformRequestContext();
    if (requestContext is null || string.IsNullOrWhiteSpace(requestContext.TenantId))
        return Results.Problem(title: "缺少租户上下文。", statusCode: StatusCodes.Status400BadRequest);

    var items = await db.AuditLogs
        .Where(x => x.TenantId == requestContext.TenantId
            && x.ResourceType == resourceType
            && x.ResourceId == resourceId)
        .OrderByDescending(x => x.CreatedAtUtc)
        .Select(x => new ContentAuditLogResponse(
            x.AuditLogId, x.TenantId, x.OperatorId, x.OperatorName,
            x.ResourceType, x.ResourceId, x.Action,
            x.BeforeSnapshotJson, x.AfterSnapshotJson, x.CreatedAtUtc))
        .ToListAsync();

    return Results.Ok(items);
}).RequireAuthorization();

// ── App Config Snapshot Endpoints ────────────────────────────

app.MapGet("/api/config/app-config", async (
    HttpContext context,
    ConfigDbContext db,
    string? locale) =>
{
    var requestContext = context.GetPlatformRequestContext();
    if (requestContext is null || string.IsNullOrWhiteSpace(requestContext.TenantId))
        return Results.Problem(title: "缺少租户上下文。", statusCode: StatusCodes.Status400BadRequest);

    var effectiveLocale = locale ?? "zh-CN";

    var texts = await db.StaticTexts
        .Where(x => x.TenantId == requestContext.TenantId && x.Locale == effectiveLocale)
        .ToDictionaryAsync(x => x.TextKey, x => x.TextValue);

    var groups = await db.OptionGroups
        .Where(x => x.TenantId == requestContext.TenantId && x.Status == "active")
        .Select(x => new { x.OptionGroupId, x.GroupCode })
        .ToListAsync();

    var groupIds = groups.Select(g => g.OptionGroupId).ToList();
    var allItems = await db.OptionItems
        .Where(x => groupIds.Contains(x.GroupId) && x.IsActive)
        .OrderBy(x => x.SortOrder)
        .ToListAsync();

    var options = new Dictionary<string, IReadOnlyList<AppConfigOptionResponse>>();
    foreach (var group in groups)
    {
        var items = allItems
            .Where(x => x.GroupId == group.OptionGroupId)
            .Select(x => new AppConfigOptionResponse(x.OptionCode, x.LabelZh, x.SortOrder))
            .ToList();
        options[group.GroupCode] = items;
    }

    var latestVersion = await db.ConfigSnapshots
        .Where(x => x.TenantId == requestContext.TenantId)
        .MaxAsync(x => (long?)x.SnapshotVersion) ?? 0;

    return Results.Ok(new AppConfigSnapshotResponse(latestVersion, texts, options));
}).RequireAuthorization();

app.Run();
