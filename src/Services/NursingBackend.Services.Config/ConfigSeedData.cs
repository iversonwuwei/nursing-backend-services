using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using NursingBackend.BuildingBlocks.Entities;

namespace NursingBackend.Services.Config;

internal static class ConfigSeedData
{
    private const string TenantId = "tenant-demo";
    private const string SeedOperatorId = "seed-system";
    private const string SeedOperatorName = "开发环境初始化";

    private static readonly StaticTextSeed[] StaticTexts =
    [
        new("ST-SEED-001", "app_family", "visit.booking_success", "zh-CN", "探视预约已成功提交，工作人员将在 24 小时内审核。", "家属端探视预约成功提示", 3, "内容管理员", ParseUtc("2026-04-03T09:30:00Z")),
        new("ST-SEED-002", "app_family", "visit.booking_success", "en-US", "Visit request submitted. Staff will review it within 24 hours.", "Family app visit booking success message", 1, "内容管理员", ParseUtc("2026-04-03T09:35:00Z")),
        new("ST-SEED-003", "app_family", "health.bp_warning", "zh-CN", "您的家人血压偏高，护理人员已关注，请留意后续沟通。", "家属端血压预警提示", 1, "健康运营", ParseUtc("2026-04-05T08:20:00Z")),
        new("ST-SEED-004", "app_nani", "task.remind_upcoming", "zh-CN", "您有一项护理任务即将开始，请及时前往执行。", "员工端任务提醒", 2, "护理主管", ParseUtc("2026-04-01T07:50:00Z")),
        new("ST-SEED-005", "app_nani", "handoff.pending", "zh-CN", "您有一条交接班记录待确认，请尽快处理。", "员工端交接班提醒", 1, "护理主管", ParseUtc("2026-04-01T08:00:00Z")),
        new("ST-SEED-006", "admin", "admission.review_required", "zh-CN", "该入住申请需要护理主管复核确认。", "后台入住审核提示", 1, "机构管理员", ParseUtc("2026-04-02T02:15:00Z")),
        new("ST-SEED-007", "admin", "device.alarm_threshold", "zh-CN", "设备报警阈值已更新，请确认新规则生效。", "后台设备阈值提示", 2, "机构管理员", ParseUtc("2026-04-02T06:40:00Z")),
        new("ST-SEED-008", "common", "auth.session_expired", "zh-CN", "登录已过期，请重新登录。", "通用会话过期提示", 1, "平台管理员", ParseUtc("2026-03-30T03:10:00Z")),
        new("ST-SEED-009", "common", "error.server_error", "zh-CN", "服务器异常，请稍后重试或联系管理员。", "通用服务端错误提示", 2, "平台管理员", ParseUtc("2026-03-30T03:20:00Z")),
        new("ST-SEED-010", "common", "error.server_error", "en-US", "Server error. Please try again later or contact support.", "Common server error message", 1, "平台管理员", ParseUtc("2026-03-30T03:25:00Z")),
    ];

    private static readonly OptionGroupSeed[] OptionGroups =
    [
        new("OG-SEED-001", "care_level", "护理等级", "老人护理等级选项", true, "active", ParseUtc("2026-04-01T01:00:00Z")),
        new("OG-SEED-002", "alert_type", "报警类型", "设备与健康报警分类", true, "active", ParseUtc("2026-04-01T01:05:00Z")),
        new("OG-SEED-003", "visit_type", "探视类型", "家属探视方式", false, "active", ParseUtc("2026-04-02T01:10:00Z")),
        new("OG-SEED-004", "payment_status", "缴费状态", "账单缴费状态", true, "active", ParseUtc("2026-04-02T01:15:00Z")),
    ];

    private static readonly OptionItemSeed[] OptionItems =
    [
        new("OI-SEED-001", "care_level", "self_care", "自理", "Self-care", 0, true, false, ParseUtc("2026-04-01T01:00:00Z")),
        new("OI-SEED-002", "care_level", "partial_care", "半自理", "Partial Care", 1, true, true, ParseUtc("2026-04-01T01:00:00Z")),
        new("OI-SEED-003", "care_level", "full_care", "全护理", "Full Care", 2, true, false, ParseUtc("2026-04-01T01:00:00Z")),
        new("OI-SEED-004", "care_level", "special_care", "特护", "Special Care", 3, true, false, ParseUtc("2026-04-01T01:00:00Z")),
        new("OI-SEED-005", "alert_type", "fall", "跌倒报警", "Fall Alert", 0, true, false, ParseUtc("2026-04-01T01:05:00Z")),
        new("OI-SEED-006", "alert_type", "device", "设备报警", "Device Alert", 1, true, false, ParseUtc("2026-04-01T01:05:00Z")),
        new("OI-SEED-007", "alert_type", "health", "健康异常", "Health Alert", 2, true, false, ParseUtc("2026-04-01T01:05:00Z")),
        new("OI-SEED-008", "alert_type", "call", "呼叫请求", "Call Request", 3, true, false, ParseUtc("2026-04-01T01:05:00Z")),
        new("OI-SEED-009", "alert_type", "geofence", "围栏越界", "Geofence", 4, false, false, ParseUtc("2026-04-01T01:05:00Z")),
        new("OI-SEED-010", "visit_type", "onsite", "到院探视", "On-site Visit", 0, true, true, ParseUtc("2026-04-02T01:10:00Z")),
        new("OI-SEED-011", "visit_type", "video", "视频探视", "Video Visit", 1, true, false, ParseUtc("2026-04-02T01:10:00Z")),
        new("OI-SEED-012", "visit_type", "outing", "外出探视", "Outing Visit", 2, true, false, ParseUtc("2026-04-02T01:10:00Z")),
        new("OI-SEED-013", "payment_status", "pending", "待支付", "Pending", 0, true, true, ParseUtc("2026-04-02T01:15:00Z")),
        new("OI-SEED-014", "payment_status", "paid", "已支付", "Paid", 1, true, false, ParseUtc("2026-04-02T01:15:00Z")),
        new("OI-SEED-015", "payment_status", "overdue", "逾期", "Overdue", 2, true, false, ParseUtc("2026-04-02T01:15:00Z")),
    ];

    private static readonly AuditLogSeed[] AuditLogs =
    [
        new("AL-SEED-001", "static_text", SeedResourceRef.StaticText("app_family", "visit.booking_success", "zh-CN"), "update",
            "{\"textValue\":\"探视预约已提交，请等待审批。\"}",
            "{\"textValue\":\"探视预约已成功提交，工作人员将在 24 小时内审核。\"}",
            "内容管理员", ParseUtc("2026-04-03T09:30:00Z")),
        new("AL-SEED-002", "static_text", SeedResourceRef.StaticText("common", "error.server_error", "zh-CN"), "update",
            "{\"textValue\":\"服务器异常，请稍后重试。\"}",
            "{\"textValue\":\"服务器异常，请稍后重试或联系管理员。\"}",
            "平台管理员", ParseUtc("2026-03-30T03:20:00Z")),
        new("AL-SEED-003", "option_group", SeedResourceRef.OptionGroup("visit_type"), "create",
            null,
            "{\"groupCode\":\"visit_type\",\"groupName\":\"探视类型\"}",
            "内容管理员", ParseUtc("2026-04-02T01:12:00Z")),
        new("AL-SEED-004", "option_item", SeedResourceRef.OptionItem("alert_type", "geofence"), "disable",
            "{\"isActive\":true}",
            "{\"isActive\":false}",
            "护理主管", ParseUtc("2026-04-04T05:05:00Z")),
        new("AL-SEED-005", "option_item", SeedResourceRef.OptionItem("care_level", "partial_care"), "update",
            "{\"labelZh\":\"半护理\",\"isDefault\":false}",
            "{\"labelZh\":\"半自理\",\"isDefault\":true}",
            "护理主管", ParseUtc("2026-04-04T05:10:00Z")),
    ];

    public static async Task SeedAsync(ConfigDbContext dbContext, CancellationToken cancellationToken = default)
    {
        var existingTexts = await dbContext.StaticTexts
            .Where(item => item.TenantId == TenantId)
            .ToListAsync(cancellationToken);
        var existingTextKeys = existingTexts
            .Select(item => ComposeStaticTextKey(item.Namespace, item.TextKey, item.Locale))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        foreach (var seed in StaticTexts)
        {
            if (existingTextKeys.Contains(ComposeStaticTextKey(seed.Namespace, seed.TextKey, seed.Locale)))
            {
                continue;
            }

            dbContext.StaticTexts.Add(new StaticTextEntity
            {
                StaticTextId = seed.Id,
                TenantId = TenantId,
                Namespace = seed.Namespace,
                TextKey = seed.TextKey,
                Locale = seed.Locale,
                TextValue = seed.TextValue,
                Description = seed.Description,
                Version = seed.Version,
                UpdatedBy = seed.UpdatedBy,
                CreatedAtUtc = seed.UpdatedAtUtc,
                UpdatedAtUtc = seed.UpdatedAtUtc,
            });
        }

        var existingGroups = await dbContext.OptionGroups
            .Where(item => item.TenantId == TenantId)
            .ToListAsync(cancellationToken);
        var existingGroupCodes = existingGroups
            .Select(item => item.GroupCode)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        foreach (var seed in OptionGroups)
        {
            if (existingGroupCodes.Contains(seed.GroupCode))
            {
                continue;
            }

            dbContext.OptionGroups.Add(new OptionGroupEntity
            {
                OptionGroupId = seed.Id,
                TenantId = TenantId,
                GroupCode = seed.GroupCode,
                GroupName = seed.GroupName,
                Description = seed.Description,
                IsSystem = seed.IsSystem,
                Status = seed.Status,
                CreatedAtUtc = seed.UpdatedAtUtc,
                UpdatedAtUtc = seed.UpdatedAtUtc,
            });
        }

        await dbContext.SaveChangesAsync(cancellationToken);

        var groupIdByCode = await dbContext.OptionGroups
            .Where(item => item.TenantId == TenantId)
            .ToDictionaryAsync(item => item.GroupCode, item => item.OptionGroupId, StringComparer.OrdinalIgnoreCase, cancellationToken);

        var existingItems = await dbContext.OptionItems
            .Where(item => groupIdByCode.Values.Contains(item.GroupId))
            .ToListAsync(cancellationToken);
        var existingItemKeys = existingItems
            .Select(item => ComposeOptionItemKey(item.GroupId, item.OptionCode))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        foreach (var seed in OptionItems)
        {
            if (!groupIdByCode.TryGetValue(seed.GroupCode, out var groupId))
            {
                continue;
            }

            if (existingItemKeys.Contains(ComposeOptionItemKey(groupId, seed.OptionCode)))
            {
                continue;
            }

            dbContext.OptionItems.Add(new OptionItemEntity
            {
                OptionItemId = seed.Id,
                GroupId = groupId,
                OptionCode = seed.OptionCode,
                LabelZh = seed.LabelZh,
                LabelEn = seed.LabelEn,
                SortOrder = seed.SortOrder,
                IsActive = seed.IsActive,
                IsDefault = seed.IsDefault,
                CreatedAtUtc = seed.UpdatedAtUtc,
                UpdatedAtUtc = seed.UpdatedAtUtc,
            });
        }

        await dbContext.SaveChangesAsync(cancellationToken);

        var textIdByRef = await dbContext.StaticTexts
            .Where(item => item.TenantId == TenantId)
            .ToDictionaryAsync(
                item => ComposeStaticTextKey(item.Namespace, item.TextKey, item.Locale),
                item => item.StaticTextId,
                StringComparer.OrdinalIgnoreCase,
                cancellationToken);
        var itemIdByRef = await dbContext.OptionItems
            .Where(item => groupIdByCode.Values.Contains(item.GroupId))
            .Join(dbContext.OptionGroups,
                item => item.GroupId,
                group => group.OptionGroupId,
                (item, group) => new { group.GroupCode, item.OptionCode, item.OptionItemId })
            .ToDictionaryAsync(
                item => ComposeOptionItemKey(item.GroupCode, item.OptionCode),
                item => item.OptionItemId,
                StringComparer.OrdinalIgnoreCase,
                cancellationToken);

        var existingAuditIds = await dbContext.AuditLogs
            .Where(item => item.TenantId == TenantId)
            .Select(item => item.AuditLogId)
            .ToListAsync(cancellationToken);
        var existingAuditIdSet = existingAuditIds.ToHashSet(StringComparer.OrdinalIgnoreCase);

        foreach (var seed in AuditLogs)
        {
            if (existingAuditIdSet.Contains(seed.Id))
            {
                continue;
            }

            var resourceId = ResolveResourceId(seed.ResourceReference, textIdByRef, groupIdByCode, itemIdByRef);
            if (resourceId is null)
            {
                continue;
            }

            dbContext.AuditLogs.Add(new ContentAuditLogEntity
            {
                AuditLogId = seed.Id,
                TenantId = TenantId,
                OperatorId = SeedOperatorId,
                OperatorName = seed.OperatorName,
                ResourceType = seed.ResourceType,
                ResourceId = resourceId,
                Action = seed.Action,
                BeforeSnapshotJson = seed.BeforeSnapshotJson,
                AfterSnapshotJson = seed.AfterSnapshotJson,
                CreatedAtUtc = seed.CreatedAtUtc,
            });
        }

        if (!await dbContext.ConfigSnapshots.AnyAsync(item => item.TenantId == TenantId && item.Namespace == "admin" && item.Locale == "zh-CN", cancellationToken))
        {
            var texts = await dbContext.StaticTexts
                .Where(item => item.TenantId == TenantId && item.Locale == "zh-CN")
                .ToDictionaryAsync(item => item.TextKey, item => item.TextValue, cancellationToken);
            var options = await dbContext.OptionGroups
                .Where(item => item.TenantId == TenantId && item.Status == "active")
                .Select(group => new
                {
                    group.GroupCode,
                    Items = dbContext.OptionItems
                        .Where(item => item.GroupId == group.OptionGroupId && item.IsActive)
                        .OrderBy(item => item.SortOrder)
                        .Select(item => new { item.OptionCode, item.LabelZh, item.SortOrder })
                        .ToList(),
                })
                .ToListAsync(cancellationToken);

            dbContext.ConfigSnapshots.Add(new AppConfigSnapshotEntity
            {
                SnapshotId = "SNAP-SEED-001",
                TenantId = TenantId,
                Namespace = "admin",
                Locale = "zh-CN",
                SnapshotVersion = 1,
                ContentJson = JsonSerializer.Serialize(new
                {
                    texts,
                    options = options.ToDictionary(
                        item => item.GroupCode,
                        item => item.Items.Select(option => new
                        {
                            code = option.OptionCode,
                            label = option.LabelZh,
                            sortOrder = option.SortOrder,
                        }))
                }),
                GeneratedAtUtc = ParseUtc("2026-04-05T09:00:00Z"),
            });
        }

        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private static string ComposeStaticTextKey(string ns, string textKey, string locale)
        => $"{ns}::{textKey}::{locale}";

    private static string ComposeOptionItemKey(string groupOrGroupId, string optionCode)
        => $"{groupOrGroupId}::{optionCode}";

    private static string? ResolveResourceId(
        SeedResourceRef resourceReference,
        IReadOnlyDictionary<string, string> textIdByRef,
        IReadOnlyDictionary<string, string> groupIdByCode,
        IReadOnlyDictionary<string, string> itemIdByRef)
    {
        return resourceReference.Kind switch
        {
            SeedResourceKind.StaticText => textIdByRef.GetValueOrDefault(ComposeStaticTextKey(resourceReference.Namespace!, resourceReference.TextKey!, resourceReference.Locale!)),
            SeedResourceKind.OptionGroup => groupIdByCode.GetValueOrDefault(resourceReference.GroupCode!),
            SeedResourceKind.OptionItem => itemIdByRef.GetValueOrDefault(ComposeOptionItemKey(resourceReference.GroupCode!, resourceReference.OptionCode!)),
            _ => null,
        };
    }

    private static DateTimeOffset ParseUtc(string value) => DateTimeOffset.Parse(value);

    private sealed record StaticTextSeed(
        string Id,
        string Namespace,
        string TextKey,
        string Locale,
        string TextValue,
        string Description,
        int Version,
        string UpdatedBy,
        DateTimeOffset UpdatedAtUtc);

    private sealed record OptionGroupSeed(
        string Id,
        string GroupCode,
        string GroupName,
        string Description,
        bool IsSystem,
        string Status,
        DateTimeOffset UpdatedAtUtc);

    private sealed record OptionItemSeed(
        string Id,
        string GroupCode,
        string OptionCode,
        string LabelZh,
        string LabelEn,
        int SortOrder,
        bool IsActive,
        bool IsDefault,
        DateTimeOffset UpdatedAtUtc);

    private sealed record AuditLogSeed(
        string Id,
        string ResourceType,
        SeedResourceRef ResourceReference,
        string Action,
        string? BeforeSnapshotJson,
        string? AfterSnapshotJson,
        string OperatorName,
        DateTimeOffset CreatedAtUtc);

    private enum SeedResourceKind
    {
        StaticText,
        OptionGroup,
        OptionItem,
    }

    private sealed record SeedResourceRef(
        SeedResourceKind Kind,
        string? Namespace,
        string? TextKey,
        string? Locale,
        string? GroupCode,
        string? OptionCode)
    {
        public static SeedResourceRef StaticText(string ns, string textKey, string locale)
            => new(SeedResourceKind.StaticText, ns, textKey, locale, null, null);

        public static SeedResourceRef OptionGroup(string groupCode)
            => new(SeedResourceKind.OptionGroup, null, null, null, groupCode, null);

        public static SeedResourceRef OptionItem(string groupCode, string optionCode)
            => new(SeedResourceKind.OptionItem, null, null, null, groupCode, optionCode);
    }
}