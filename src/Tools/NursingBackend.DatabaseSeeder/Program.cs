using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using NursingBackend.BuildingBlocks.Entities;
using NursingBackend.BuildingBlocks.Persistence;
using NursingBackend.Services.Billing;
using NursingBackend.Services.Care;
using NursingBackend.Services.Elder;
using NursingBackend.Services.Health;
using NursingBackend.Services.Notification;
using NursingBackend.Services.Operations;
using NursingBackend.Services.Visit;

const string TenantId = "tenant-demo";

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
var serviceProvider = scope.ServiceProvider;
var logger = serviceProvider.GetRequiredService<ILoggerFactory>().CreateLogger("DatabaseSeeder");

await SeedEldersAsync(serviceProvider.GetRequiredService<ElderDbContext>(), logger);
await SeedHealthAsync(serviceProvider.GetRequiredService<HealthDbContext>(), logger);
await SeedCareAsync(serviceProvider.GetRequiredService<CareDbContext>(), logger);
await SeedVisitsAsync(serviceProvider.GetRequiredService<VisitDbContext>(), logger);
await SeedBillingAsync(serviceProvider.GetRequiredService<BillingDbContext>(), logger);
await SeedNotificationsAsync(serviceProvider.GetRequiredService<NotificationDbContext>(), logger);
await SeedOperationsAsync(serviceProvider.GetRequiredService<OperationsDbContext>(), logger);

logger.LogInformation("All database seed operations completed successfully.");

static DateTimeOffset SeedTimestamp(int year, int month, int day, int hour, int minute)
{
	return new DateTimeOffset(year, month, day, hour, minute, 0, TimeSpan.FromHours(8)).ToUniversalTime();
}

static async Task SeedEldersAsync(ElderDbContext dbContext, ILogger logger)
{
	var profiles = new[]
	{
		new ElderProfileEntity
		{
			ElderId = "E001",
			TenantId = TenantId,
			ElderName = "王建国",
			Age = 82,
			Gender = "male",
			CareLevel = "L3",
			RoomNumber = "A-1203",
			FamilyContactName = "王敏",
			FamilyContactPhone = "13800000001",
			MedicalAlerts = ["高血压", "夜间跌倒风险"],
			AdmissionStatus = "Active",
		},
		new ElderProfileEntity
		{
			ElderId = "E002",
			TenantId = TenantId,
			ElderName = "刘秀兰",
			Age = 79,
			Gender = "female",
			CareLevel = "L2",
			RoomNumber = "B-0806",
			FamilyContactName = "陈芳",
			FamilyContactPhone = "13800000002",
			MedicalAlerts = ["夜间离床", "轻度认知障碍"],
			AdmissionStatus = "Active",
		},
		new ElderProfileEntity
		{
			ElderId = "E003",
			TenantId = TenantId,
			ElderName = "陈志华",
			Age = 85,
			Gender = "male",
			CareLevel = "L4",
			RoomNumber = "C-1502",
			FamilyContactName = "陈力",
			FamilyContactPhone = "13800000003",
			MedicalAlerts = ["低氧风险", "糖尿病"],
			AdmissionStatus = "Active",
		},
	};

	var admissions = new[]
	{
		new AdmissionRecordEntity
		{
			AdmissionId = "ADM-001",
			TenantId = TenantId,
			ElderId = "E001",
			AdmissionReference = "入住-2026-001",
			Status = "Completed",
			CareLevel = "L3",
			RoomNumber = "A-1203",
			CreatedAtUtc = SeedTimestamp(2026, 4, 10, 9, 0),
		},
		new AdmissionRecordEntity
		{
			AdmissionId = "ADM-002",
			TenantId = TenantId,
			ElderId = "E002",
			AdmissionReference = "入住-2026-002",
			Status = "Completed",
			CareLevel = "L2",
			RoomNumber = "B-0806",
			CreatedAtUtc = SeedTimestamp(2026, 4, 11, 10, 30),
		},
		new AdmissionRecordEntity
		{
			AdmissionId = "ADM-003",
			TenantId = TenantId,
			ElderId = "E003",
			AdmissionReference = "入住-2026-003",
			Status = "Completed",
			CareLevel = "L4",
			RoomNumber = "C-1502",
			CreatedAtUtc = SeedTimestamp(2026, 4, 12, 14, 0),
		},
	};

	var inserted = 0;
	var updated = 0;
	foreach (var profile in profiles)
	{
		var existing = await dbContext.Elders.FindAsync(profile.ElderId);
		if (existing is null)
		{
			await dbContext.Elders.AddAsync(profile);
			inserted++;
			continue;
		}

		existing.CareLevel = profile.CareLevel;
		existing.RoomNumber = profile.RoomNumber;
		existing.AdmissionStatus = profile.AdmissionStatus;
		updated++;
	}

	foreach (var admission in admissions)
	{
		var existing = await dbContext.Admissions.FindAsync(admission.AdmissionId);
		if (existing is null)
		{
			await dbContext.Admissions.AddAsync(admission);
			inserted++;
			continue;
		}

		existing.Status = admission.Status;
		updated++;
	}

	await dbContext.SaveChangesAsync();
	logger.LogInformation("Seeded elder data. inserted={Inserted} updated={Updated}", inserted, updated);
}

static async Task SeedHealthAsync(HealthDbContext dbContext, ILogger logger)
{
	var archives = new[]
	{
		new HealthArchiveEntity
		{
			ElderId = "E001",
			TenantId = TenantId,
			ElderName = "王建国",
			BloodPressure = "138/85",
			HeartRate = 78,
			Temperature = 36.7m,
			BloodSugar = 6.2m,
			Oxygen = 96,
			RiskSummary = "夜间跌倒风险较高，建议加强 22:00 后离床巡检。",
			UpdatedAtUtc = SeedTimestamp(2026, 4, 13, 8, 20),
		},
		new HealthArchiveEntity
		{
			ElderId = "E002",
			TenantId = TenantId,
			ElderName = "刘秀兰",
			BloodPressure = "132/80",
			HeartRate = 81,
			Temperature = 36.5m,
			BloodSugar = 5.9m,
			Oxygen = 97,
			RiskSummary = "离床预警较频繁，建议结合夜班巡视与睡眠评估。",
			UpdatedAtUtc = SeedTimestamp(2026, 4, 13, 7, 50),
		},
		new HealthArchiveEntity
		{
			ElderId = "E003",
			TenantId = TenantId,
			ElderName = "陈志华",
			BloodPressure = "145/92",
			HeartRate = 92,
			Temperature = 37.1m,
			BloodSugar = 8.8m,
			Oxygen = 90,
			RiskSummary = "血氧偏低且血糖波动，需要复测并观察医生随访。",
			UpdatedAtUtc = SeedTimestamp(2026, 4, 13, 8, 5),
		},
	};

	var inserted = 0;
	var updated = 0;
	foreach (var archive in archives)
	{
		var existing = await dbContext.HealthArchives.FindAsync(archive.ElderId);
		if (existing is null)
		{
			await dbContext.HealthArchives.AddAsync(archive);
			inserted++;
			continue;
		}

		existing.BloodPressure = archive.BloodPressure;
		existing.HeartRate = archive.HeartRate;
		existing.Temperature = archive.Temperature;
		existing.BloodSugar = archive.BloodSugar;
		existing.Oxygen = archive.Oxygen;
		existing.RiskSummary = archive.RiskSummary;
		existing.UpdatedAtUtc = archive.UpdatedAtUtc;
		updated++;
	}

	await dbContext.SaveChangesAsync();
	logger.LogInformation("Seeded health data. inserted={Inserted} updated={Updated}", inserted, updated);
}

static async Task SeedCareAsync(CareDbContext dbContext, ILogger logger)
{
	var plans = new[]
	{
		new CarePlanEntity
		{
			CarePlanId = "CP-E001",
			TenantId = TenantId,
			ElderId = "E001",
			ElderName = "王建国",
			PlanLevel = "L3",
			Status = "Active",
			GeneratedAtUtc = SeedTimestamp(2026, 4, 13, 7, 30),
		},
		new CarePlanEntity
		{
			CarePlanId = "CP-E002",
			TenantId = TenantId,
			ElderId = "E002",
			ElderName = "刘秀兰",
			PlanLevel = "L2",
			Status = "Active",
			GeneratedAtUtc = SeedTimestamp(2026, 4, 13, 7, 20),
		},
		new CarePlanEntity
		{
			CarePlanId = "CP-E003",
			TenantId = TenantId,
			ElderId = "E003",
			ElderName = "陈志华",
			PlanLevel = "L4",
			Status = "Escalated",
			GeneratedAtUtc = SeedTimestamp(2026, 4, 13, 7, 15),
		},
	};

	var tasks = new[]
	{
		new CareTaskEntity { TaskId = "CT-E001-01", TenantId = TenantId, ElderId = "E001", Title = "08:30 晨间巡查", AssigneeRole = "nurse", DueAtLabel = "08:30", Status = "Pending" },
		new CareTaskEntity { TaskId = "CT-E001-02", TenantId = TenantId, ElderId = "E001", Title = "10:00 血压复测", AssigneeRole = "nurse", DueAtLabel = "10:00", Status = "Pending" },
		new CareTaskEntity { TaskId = "CT-E002-01", TenantId = TenantId, ElderId = "E002", Title = "09:00 离床风险回访", AssigneeRole = "caregiver", DueAtLabel = "09:00", Status = "InProgress" },
		new CareTaskEntity { TaskId = "CT-E003-01", TenantId = TenantId, ElderId = "E003", Title = "08:15 血氧复测", AssigneeRole = "nurse", DueAtLabel = "08:15", Status = "Pending" },
		new CareTaskEntity { TaskId = "CT-E003-02", TenantId = TenantId, ElderId = "E003", Title = "09:30 医生联动确认", AssigneeRole = "doctor", DueAtLabel = "09:30", Status = "Pending" },
	};

	var packages = new[]
	{
		new ServicePackageEntity
		{
			PackageId = "PKG-L3-STANDARD",
			TenantId = TenantId,
			Name = "L3 标准照护包",
			CareLevel = "L3",
			TargetGroup = "机构养老",
			MonthlyPrice = "6800",
			SettlementCycle = "Monthly",
			ServiceScopeJson = "[\"生命体征监测\",\"夜间巡查\",\"药物提醒\"]",
			AddOnsJson = "[\"医生协同\",\"康复训练\"]",
			BoundElders = 2,
			Status = "Published",
			CreatedAtUtc = SeedTimestamp(2026, 4, 10, 10, 0),
			PublishedAtUtc = SeedTimestamp(2026, 4, 11, 9, 0),
			PricingNote = "用于本地联调样本",
		},
	};

	var servicePlans = new[]
	{
		new ServicePlanEntity
		{
			PlanId = "SP-E001",
			TenantId = TenantId,
			PackageId = "PKG-L3-STANDARD",
			PackageName = "L3 标准照护包",
			ElderlyName = "王建国",
			Room = "A-1203",
			CareLevel = "L3",
			Focus = "夜间离床与血压监测",
			ShiftSummary = "白班重点复测血压，夜班加强离床巡视。",
			OwnerRole = "nurse_lead",
			OwnerName = "李护士长",
			RiskTagsJson = "[\"fall-risk\",\"blood-pressure\"]",
			Source = "database-seeder",
			Status = "Published",
			CreatedAtUtc = SeedTimestamp(2026, 4, 12, 15, 0),
			ReviewNote = "联调示例计划",
		},
	};

	var assignments = new[]
	{
		new ServicePlanAssignmentEntity
		{
			AssignmentId = "SPA-E001-01",
			TenantId = TenantId,
			PlanId = "SP-E001",
			ElderlyName = "王建国",
			PackageName = "L3 标准照护包",
			Room = "A-1203",
			StaffName = "赵护工",
			StaffRole = "caregiver",
			EmploymentSource = "self-owned",
			DayLabel = "2026-04-13",
			Shift = "day",
			Status = "Assigned",
			CreatedAtUtc = SeedTimestamp(2026, 4, 13, 7, 0),
		},
	};

	var audits = new[]
	{
		new CareWorkflowAuditEntity
		{
			AuditId = "AUD-SP-E001-01",
			TenantId = TenantId,
			AggregateType = "ServicePlan",
			AggregateId = "SP-E001",
			ActionType = "Published",
			OperatorUserId = "ops-admin",
			OperatorUserName = "运营管理员",
			CorrelationId = "seed-correlation-care-001",
			DetailJson = "{\"source\":\"database-seeder\"}",
			CreatedAtUtc = SeedTimestamp(2026, 4, 12, 15, 5),
		},
	};

	var inserted = 0;
	var updated = 0;
	inserted += await UpsertCarePlansAsync(dbContext, plans, () => updated++);
	inserted += await UpsertCareTasksAsync(dbContext, tasks, () => updated++);
	inserted += await UpsertServicePackagesAsync(dbContext, packages, () => updated++);
	inserted += await UpsertServicePlansAsync(dbContext, servicePlans, () => updated++);
	inserted += await UpsertServiceAssignmentsAsync(dbContext, assignments, () => updated++);
	inserted += await UpsertCareAuditsAsync(dbContext, audits, () => updated++);
	await dbContext.SaveChangesAsync();
	logger.LogInformation("Seeded care data. inserted={Inserted} updated={Updated}", inserted, updated);
}

static async Task SeedVisitsAsync(VisitDbContext dbContext, ILogger logger)
{
	var appointments = new[]
	{
		new VisitAppointmentEntity
		{
			VisitId = "VIS-001",
			TenantId = TenantId,
			ElderId = "E001",
			VisitorName = "王敏",
			Relation = "daughter",
			Phone = "13800000001",
			PlannedAtUtc = SeedTimestamp(2026, 4, 14, 14, 0),
			VisitType = "onsite",
			Notes = "家属探访并确认近期血压观察情况。",
			Status = "Approved",
		},
		new VisitAppointmentEntity
		{
			VisitId = "VIS-002",
			TenantId = TenantId,
			ElderId = "E002",
			VisitorName = "陈芳",
			Relation = "daughter",
			Phone = "13800000002",
			PlannedAtUtc = SeedTimestamp(2026, 4, 15, 10, 30),
			VisitType = "video",
			Notes = "视频探访，关注夜间睡眠状态。",
			Status = "Submitted",
		},
	};

	var inserted = 0;
	var updated = 0;
	foreach (var appointment in appointments)
	{
		var existing = await dbContext.VisitAppointments.FindAsync(appointment.VisitId);
		if (existing is null)
		{
			await dbContext.VisitAppointments.AddAsync(appointment);
			inserted++;
			continue;
		}

		existing.Status = appointment.Status;
		updated++;
	}

	await dbContext.SaveChangesAsync();
	logger.LogInformation("Seeded visit data. inserted={Inserted} updated={Updated}", inserted, updated);
}

static async Task SeedBillingAsync(BillingDbContext dbContext, ILogger logger)
{
	var invoices = new[]
	{
		new BillingInvoiceEntity
		{
			InvoiceId = "INV-001",
			TenantId = TenantId,
			ElderId = "E001",
			ElderName = "王建国",
			PackageName = "L3 标准照护包",
			Amount = 6800m,
			DueAtUtc = SeedTimestamp(2026, 4, 20, 23, 59),
			Status = "Issued",
			NotificationStatus = "Delivered",
			CreatedAtUtc = SeedTimestamp(2026, 4, 13, 8, 0),
		},
		new BillingInvoiceEntity
		{
			InvoiceId = "INV-002",
			TenantId = TenantId,
			ElderId = "E002",
			ElderName = "刘秀兰",
			PackageName = "L2 基础照护包",
			Amount = 5200m,
			DueAtUtc = SeedTimestamp(2026, 4, 5, 23, 59),
			Status = "Issued",
			NotificationStatus = "Failed",
			LastNotificationFailureCode = "SMS_TIMEOUT",
			LastNotificationFailureReason = "短信通道超时",
			CreatedAtUtc = SeedTimestamp(2026, 4, 1, 9, 30),
		},
		new BillingInvoiceEntity
		{
			InvoiceId = "INV-003",
			TenantId = TenantId,
			ElderId = "E003",
			ElderName = "陈志华",
			PackageName = "L4 加强照护包",
			Amount = 8800m,
			DueAtUtc = SeedTimestamp(2026, 4, 18, 23, 59),
			Status = "ActionRequired",
			NotificationStatus = "Failed",
			LastNotificationFailureCode = "PUSH_REJECTED",
			LastNotificationFailureReason = "推送供应商拒收",
			CreatedAtUtc = SeedTimestamp(2026, 4, 13, 8, 10),
		},
	};

	var inserted = 0;
	var updated = 0;
	foreach (var invoice in invoices)
	{
		var existing = await dbContext.Invoices.FindAsync(invoice.InvoiceId);
		if (existing is null)
		{
			await dbContext.Invoices.AddAsync(invoice);
			inserted++;
			continue;
		}

		existing.Status = invoice.Status;
		existing.NotificationStatus = invoice.NotificationStatus;
		existing.LastNotificationFailureCode = invoice.LastNotificationFailureCode;
		existing.LastNotificationFailureReason = invoice.LastNotificationFailureReason;
		existing.UpdatedAtUtc = SeedTimestamp(2026, 4, 13, 8, 15);
		updated++;
	}

	await dbContext.SaveChangesAsync();
	logger.LogInformation("Seeded billing data. inserted={Inserted} updated={Updated}", inserted, updated);
}

static async Task SeedNotificationsAsync(NotificationDbContext dbContext, ILogger logger)
{
	var notifications = new[]
	{
		new NotificationMessageEntity
		{
			NotificationId = "NTF-001",
			TenantId = TenantId,
			Audience = "family",
			AudienceKey = "E001",
			Category = "visit_notice",
			Title = "探访预约已确认",
			Body = "王建国的现场探访已预约在 04/14 14:00。",
			SourceService = "visit-service",
			SourceEntityId = "VIS-001",
			CorrelationId = "seed-notification-001",
			Status = "Delivered",
			CreatedAtUtc = SeedTimestamp(2026, 4, 13, 8, 25),
		},
		new NotificationMessageEntity
		{
			NotificationId = "NTF-002",
			TenantId = TenantId,
			Audience = "nani",
			AudienceKey = "E001",
			Category = "scheduled_reminder",
			Title = "10:00 血压复测提醒",
			Body = "请在 10:00 前完成王建国的血压复测并回填记录。",
			SourceService = "care-service",
			SourceEntityId = "CT-E001-02",
			CorrelationId = "seed-notification-002",
			Status = "Queued",
			CreatedAtUtc = SeedTimestamp(2026, 4, 13, 8, 18),
		},
		new NotificationMessageEntity
		{
			NotificationId = "NTF-003",
			TenantId = TenantId,
			Audience = "tenant",
			AudienceKey = TenantId,
			Category = "system_broadcast",
			Title = "夜班交接注意低氧监测",
			Body = "陈志华血氧波动较大，请夜班关注复测结果。",
			SourceService = "operations-service",
			SourceEntityId = "ALT-003",
			CorrelationId = "seed-notification-003",
			Status = "Failed",
			CreatedAtUtc = SeedTimestamp(2026, 4, 13, 8, 12),
		},
	};

	var inserted = 0;
	var updated = 0;
	foreach (var notification in notifications)
	{
		var existing = await dbContext.Notifications.FindAsync(notification.NotificationId);
		if (existing is null)
		{
			await dbContext.Notifications.AddAsync(notification);
			inserted++;
			continue;
		}

		existing.Status = notification.Status;
		existing.Title = notification.Title;
		existing.Body = notification.Body;
		updated++;
	}

	await dbContext.SaveChangesAsync();
	logger.LogInformation("Seeded notification data. inserted={Inserted} updated={Updated}", inserted, updated);
}

static async Task SeedOperationsAsync(OperationsDbContext dbContext, ILogger logger)
{
	var alerts = SeedAlerts();
	var inserted = 0;
	var updated = 0;
	foreach (var alert in alerts)
	{
		var existing = await dbContext.AlertCases.FindAsync(alert.AlertId);
		if (existing is null)
		{
			await dbContext.AlertCases.AddAsync(alert);
			inserted++;
			continue;
		}

		existing.Status = alert.Status;
		existing.Level = alert.Level;
		existing.Description = alert.Description;
		existing.DeviceName = alert.DeviceName;
		existing.HandledBy = alert.HandledBy;
		existing.HandledAtUtc = alert.HandledAtUtc;
		existing.Resolution = alert.Resolution;
		updated++;
	}

	await dbContext.SaveChangesAsync();
	logger.LogInformation("Seeded operations alert data. inserted={Inserted} updated={Updated}", inserted, updated);
}

static AlertCaseEntity[] SeedAlerts()
{
	return
	[
		new AlertCaseEntity
		{
			AlertId = "ALT-001",
			TenantId = TenantId,
			Module = "emergency_call",
			Type = "call",
			Level = "critical",
			Status = "pending",
			ElderId = "E001",
			ElderlyName = "王建国",
			RoomNumber = "A-1203",
			Description = "床旁呼叫器连续呼叫，需要立即到场确认。",
			OccurredAtUtc = SeedTimestamp(2026, 4, 13, 8, 12),
		},
		new AlertCaseEntity
		{
			AlertId = "ALT-002",
			TenantId = TenantId,
			Module = "bed_exit",
			Type = "bedExit",
			Level = "warning",
			Status = "processing",
			ElderId = "E002",
			ElderlyName = "刘秀兰",
			RoomNumber = "B-0806",
			Description = "夜间离床超过 8 分钟，巡查人员已出发。",
			DeviceName = "离床传感器",
			OccurredAtUtc = SeedTimestamp(2026, 4, 13, 7, 48),
			HandledBy = "夜班护士",
			HandledAtUtc = SeedTimestamp(2026, 4, 13, 7, 55),
		},
		new AlertCaseEntity
		{
			AlertId = "ALT-003",
			TenantId = TenantId,
			Module = "anomaly",
			Type = "health",
			Level = "critical",
			Status = "pending",
			ElderId = "E003",
			ElderlyName = "陈志华",
			RoomNumber = "C-1502",
			Description = "血氧持续低于阈值，需要复测并考虑升级医生联动。",
			DeviceName = "生命体征监测带",
			OccurredAtUtc = SeedTimestamp(2026, 4, 13, 8, 6),
		},
		new AlertCaseEntity
		{
			AlertId = "ALT-004",
			TenantId = TenantId,
			Module = "sos",
			Type = "sos",
			Level = "critical",
			Status = "resolved",
			ElderId = "E004",
			ElderlyName = "赵美玲",
			RoomNumber = "D-0901",
			Description = "SOS 一键求助已处置，家属与医生均已同步。",
			DeviceName = "SOS 按钮",
			OccurredAtUtc = SeedTimestamp(2026, 4, 13, 6, 42),
			HandledBy = "值班主管",
			HandledAtUtc = SeedTimestamp(2026, 4, 13, 6, 55),
			Resolution = "已完成到场安抚、医生联动与家属通知。",
		},
		new AlertCaseEntity
		{
			AlertId = "ALT-005",
			TenantId = TenantId,
			Module = "anomaly",
			Type = "device",
			Level = "warning",
			Status = "processing",
			ElderId = "E005",
			ElderlyName = "周德明",
			RoomNumber = "A-0908",
			Description = "床旁生命体征设备离线，需要设备组复位。",
			DeviceName = "监护主机",
			OccurredAtUtc = SeedTimestamp(2026, 4, 13, 7, 32),
			HandledBy = "设备专员",
			HandledAtUtc = SeedTimestamp(2026, 4, 13, 7, 40),
		},
	];
}

static async Task<int> UpsertCarePlansAsync(CareDbContext dbContext, IEnumerable<CarePlanEntity> items, Action onUpdate)
{
	var inserted = 0;
	foreach (var item in items)
	{
		var existing = await dbContext.CarePlans.FindAsync(item.CarePlanId);
		if (existing is null)
		{
			await dbContext.CarePlans.AddAsync(item);
			inserted++;
			continue;
		}

		existing.Status = item.Status;
		onUpdate();
	}

	return inserted;
}

static async Task<int> UpsertCareTasksAsync(CareDbContext dbContext, IEnumerable<CareTaskEntity> items, Action onUpdate)
{
	var inserted = 0;
	foreach (var item in items)
	{
		var existing = await dbContext.CareTasks.FindAsync(item.TaskId);
		if (existing is null)
		{
			await dbContext.CareTasks.AddAsync(item);
			inserted++;
			continue;
		}

		existing.Status = item.Status;
		onUpdate();
	}

	return inserted;
}

static async Task<int> UpsertServicePackagesAsync(CareDbContext dbContext, IEnumerable<ServicePackageEntity> items, Action onUpdate)
{
	var inserted = 0;
	foreach (var item in items)
	{
		var existing = await dbContext.ServicePackages.FindAsync(item.PackageId);
		if (existing is null)
		{
			await dbContext.ServicePackages.AddAsync(item);
			inserted++;
			continue;
		}

		existing.Name = item.Name;
		existing.CareLevel = item.CareLevel;
		existing.TargetGroup = item.TargetGroup;
		existing.MonthlyPrice = item.MonthlyPrice;
		existing.SettlementCycle = item.SettlementCycle;
		existing.ServiceScopeJson = item.ServiceScopeJson;
		existing.AddOnsJson = item.AddOnsJson;
		existing.BoundElders = item.BoundElders;
		existing.Status = item.Status;
		existing.PublishedAtUtc = item.PublishedAtUtc;
		existing.PricingNote = item.PricingNote;
		onUpdate();
	}

	return inserted;
}

static async Task<int> UpsertServicePlansAsync(CareDbContext dbContext, IEnumerable<ServicePlanEntity> items, Action onUpdate)
{
	var inserted = 0;
	foreach (var item in items)
	{
		var existing = await dbContext.ServicePlans.FindAsync(item.PlanId);
		if (existing is null)
		{
			await dbContext.ServicePlans.AddAsync(item);
			inserted++;
			continue;
		}

		existing.PackageId = item.PackageId;
		existing.PackageName = item.PackageName;
		existing.ElderlyName = item.ElderlyName;
		existing.Room = item.Room;
		existing.CareLevel = item.CareLevel;
		existing.Focus = item.Focus;
		existing.ShiftSummary = item.ShiftSummary;
		existing.OwnerRole = item.OwnerRole;
		existing.OwnerName = item.OwnerName;
		existing.RiskTagsJson = item.RiskTagsJson;
		existing.Source = item.Source;
		existing.Status = item.Status;
		existing.ReviewNote = item.ReviewNote;
		onUpdate();
	}

	return inserted;
}

static async Task<int> UpsertServiceAssignmentsAsync(CareDbContext dbContext, IEnumerable<ServicePlanAssignmentEntity> items, Action onUpdate)
{
	var inserted = 0;
	foreach (var item in items)
	{
		var existing = await dbContext.ServicePlanAssignments.FindAsync(item.AssignmentId);
		if (existing is null)
		{
			await dbContext.ServicePlanAssignments.AddAsync(item);
			inserted++;
			continue;
		}

		existing.ElderlyName = item.ElderlyName;
		existing.PackageName = item.PackageName;
		existing.Room = item.Room;
		existing.StaffName = item.StaffName;
		existing.StaffRole = item.StaffRole;
		existing.EmploymentSource = item.EmploymentSource;
		existing.PartnerAgencyName = item.PartnerAgencyName;
		existing.DayLabel = item.DayLabel;
		existing.Shift = item.Shift;
		existing.Status = item.Status;
		onUpdate();
	}

	return inserted;
}

static async Task<int> UpsertCareAuditsAsync(CareDbContext dbContext, IEnumerable<CareWorkflowAuditEntity> items, Action onUpdate)
{
	var inserted = 0;
	foreach (var item in items)
	{
		var existing = await dbContext.CareWorkflowAudits.FindAsync(item.AuditId);
		if (existing is null)
		{
			await dbContext.CareWorkflowAudits.AddAsync(item);
			inserted++;
			continue;
		}

		onUpdate();
	}

	return inserted;
}