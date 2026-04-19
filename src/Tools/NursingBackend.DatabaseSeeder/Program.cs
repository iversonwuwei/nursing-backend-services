using System.Text.Json;
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
using NursingBackend.Services.Organization;
using NursingBackend.Services.Rooms;
using NursingBackend.Services.Staffing;
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
var serviceProvider = scope.ServiceProvider;
var logger = serviceProvider.GetRequiredService<ILoggerFactory>().CreateLogger("DatabaseSeeder");

await SeedEldersAsync(serviceProvider.GetRequiredService<ElderDbContext>(), logger);
await SeedOrganizationsAsync(serviceProvider.GetRequiredService<OrganizationDbContext>(), logger);
await SeedRoomsAsync(serviceProvider.GetRequiredService<RoomsDbContext>(), logger);
await SeedStaffingAsync(serviceProvider.GetRequiredService<StaffingDbContext>(), logger);
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

static async Task SeedOrganizationsAsync(OrganizationDbContext dbContext, ILogger logger)
{
	var organizations = new[]
	{
		new OrganizationEntity
		{
			OrganizationId = "ORG-PD-01",
			TenantId = TenantId,
			Name = "浦东康养中心",
			Address = "浦东新区康桥路 88 号",
			Phone = "021-88886666",
			Status = "运营中",
			EstablishedDate = "2024-09-01",
			Manager = "张院长",
			ManagerPhone = "13800138000",
			Description = "承担机构养老入住与护理资源调度的主院区。",
			LifecycleStatus = "已启用",
			CreatedAtUtc = SeedTimestamp(2026, 4, 15, 9, 0),
			ActivatedAtUtc = SeedTimestamp(2026, 4, 15, 10, 0),
			ActivationNote = "已完成现场核验并进入正式运营。",
		},
		new OrganizationEntity
		{
			OrganizationId = "ORG-JA-01",
			TenantId = TenantId,
			Name = "静安护理院",
			Address = "静安区江宁路 108 号",
			Phone = "021-66668888",
			Status = "运营中",
			EstablishedDate = "2025-03-18",
			Manager = "李主任",
			ManagerPhone = "13800138010",
			Description = "承接高龄照护与短住观察的辅助院区。",
			LifecycleStatus = "已启用",
			CreatedAtUtc = SeedTimestamp(2026, 4, 15, 9, 30),
			ActivatedAtUtc = SeedTimestamp(2026, 4, 15, 11, 0),
			ActivationNote = "已完成跨部门交接并开放床位资源池。",
		},
	};

	var inserted = 0;
	var updated = 0;
	foreach (var organization in organizations)
	{
		var existing = await dbContext.Organizations.FindAsync(organization.OrganizationId);
		if (existing is null)
		{
			await dbContext.Organizations.AddAsync(organization);
			inserted++;
			continue;
		}

		existing.Name = organization.Name;
		existing.Address = organization.Address;
		existing.Phone = organization.Phone;
		existing.Status = organization.Status;
		existing.EstablishedDate = organization.EstablishedDate;
		existing.Manager = organization.Manager;
		existing.ManagerPhone = organization.ManagerPhone;
		existing.Description = organization.Description;
		existing.LifecycleStatus = organization.LifecycleStatus;
		existing.ActivatedAtUtc = organization.ActivatedAtUtc;
		existing.ActivationNote = organization.ActivationNote;
		updated++;
	}

	await dbContext.SaveChangesAsync();
	logger.LogInformation("Seeded organization data. inserted={Inserted} updated={Updated}", inserted, updated);
}

static async Task SeedRoomsAsync(RoomsDbContext dbContext, ILogger logger)
{
	var rooms = new[]
	{
		new RoomEntity
		{
			RoomId = "A-1203",
			TenantId = TenantId,
			Name = "A 区 1203",
			Floor = 12,
			FloorName = "12楼",
			Type = "双人间",
			Capacity = 2,
			Status = "可入住",
			OrganizationId = "ORG-PD-01",
			OrganizationName = "浦东康养中心",
			FacilitiesJson = JsonSerializer.Serialize(new[] { "独立卫浴", "适老扶手" }),
			CleanStatus = "已清洁",
			LastClean = "2026-04-16 07:00",
			NextClean = "2026-04-18 07:00",
			LifecycleStatus = "已启用",
			CreatedAtUtc = SeedTimestamp(2026, 4, 15, 10, 0),
			ActivatedAtUtc = SeedTimestamp(2026, 4, 15, 10, 15),
			ActivationNote = "已纳入入住资源池。",
		},
		new RoomEntity
		{
			RoomId = "B-0806",
			TenantId = TenantId,
			Name = "B 区 0806",
			Floor = 8,
			FloorName = "8楼",
			Type = "双人间",
			Capacity = 2,
			Status = "可入住",
			OrganizationId = "ORG-PD-01",
			OrganizationName = "浦东康养中心",
			FacilitiesJson = JsonSerializer.Serialize(new[] { "护理呼叫器", "无障碍洗手台" }),
			CleanStatus = "已清洁",
			LastClean = "2026-04-16 07:30",
			NextClean = "2026-04-18 07:30",
			LifecycleStatus = "已启用",
			CreatedAtUtc = SeedTimestamp(2026, 4, 15, 10, 5),
			ActivatedAtUtc = SeedTimestamp(2026, 4, 15, 10, 20),
			ActivationNote = "已纳入入住资源池。",
		},
		new RoomEntity
		{
			RoomId = "C-1502",
			TenantId = TenantId,
			Name = "C 区 1502",
			Floor = 15,
			FloorName = "15楼",
			Type = "单人间",
			Capacity = 1,
			Status = "可入住",
			OrganizationId = "ORG-JA-01",
			OrganizationName = "静安护理院",
			FacilitiesJson = JsonSerializer.Serialize(new[] { "低氧监测", "独立卫浴" }),
			CleanStatus = "已清洁",
			LastClean = "2026-04-16 08:00",
			NextClean = "2026-04-18 08:00",
			LifecycleStatus = "已启用",
			CreatedAtUtc = SeedTimestamp(2026, 4, 15, 10, 10),
			ActivatedAtUtc = SeedTimestamp(2026, 4, 15, 10, 25),
			ActivationNote = "已纳入重点观察房资源池。",
		},
		new RoomEntity
		{
			RoomId = "PD-1001",
			TenantId = TenantId,
			Name = "PD 区 1001",
			Floor = 10,
			FloorName = "10楼",
			Type = "双人间",
			Capacity = 2,
			Status = "可入住",
			OrganizationId = "ORG-PD-01",
			OrganizationName = "浦东康养中心",
			FacilitiesJson = JsonSerializer.Serialize(new[] { "阳台", "护理呼叫器" }),
			CleanStatus = "已清洁",
			LastClean = "2026-04-16 09:00",
			NextClean = "2026-04-18 09:00",
			LifecycleStatus = "已启用",
			CreatedAtUtc = SeedTimestamp(2026, 4, 15, 10, 15),
			ActivatedAtUtc = SeedTimestamp(2026, 4, 15, 10, 30),
			ActivationNote = "保留为空房样本，用于联调空床分配。",
		},
	};

	var inserted = 0;
	var updated = 0;
	foreach (var room in rooms)
	{
		var existing = await dbContext.Rooms.FindAsync(room.RoomId);
		if (existing is null)
		{
			await dbContext.Rooms.AddAsync(room);
			inserted++;
			continue;
		}

		existing.Name = room.Name;
		existing.Floor = room.Floor;
		existing.FloorName = room.FloorName;
		existing.Type = room.Type;
		existing.Capacity = room.Capacity;
		existing.Status = room.Status;
		existing.OrganizationId = room.OrganizationId;
		existing.OrganizationName = room.OrganizationName;
		existing.FacilitiesJson = room.FacilitiesJson;
		existing.CleanStatus = room.CleanStatus;
		existing.LastClean = room.LastClean;
		existing.NextClean = room.NextClean;
		existing.LifecycleStatus = room.LifecycleStatus;
		existing.ActivatedAtUtc = room.ActivatedAtUtc;
		existing.ActivationNote = room.ActivationNote;
		updated++;
	}

	await dbContext.SaveChangesAsync();
	logger.LogInformation("Seeded rooms data. inserted={Inserted} updated={Updated}", inserted, updated);
}

static async Task SeedStaffingAsync(StaffingDbContext dbContext, ILogger logger)
{
	var defaultScheduleJson = JsonSerializer.Serialize(new[]
	{
		new { Day = "周一", Shift = "白班" },
		new { Day = "周二", Shift = "白班" },
		new { Day = "周三", Shift = "休息" },
		new { Day = "周四", Shift = "白班" },
		new { Day = "周五", Shift = "夜班" },
		new { Day = "周六", Shift = "休息" },
		new { Day = "周日", Shift = "白班" },
	});
	var certificatesJson = JsonSerializer.Serialize(new[] { "养老护理员证", "急救证" });

	var staffMembers = new[]
	{
		new StaffMemberEntity
		{
			StaffId = "STF-PD-001",
			TenantId = TenantId,
			Name = "李护理",
			Role = "护理员",
			Department = "护理部",
			OrganizationId = "ORG-PD-01",
			OrganizationName = "浦东康养中心",
			EmploymentSource = "自有团队",
			Phone = "13800138021",
			Status = "在职",
			Gender = "女",
			Email = "li.nurse@nursing.local",
			Age = 29,
			Performance = 92,
			Attendance = 97,
			Satisfaction = 94,
			HireDate = "2025-08-01",
			ScheduleJson = defaultScheduleJson,
			CertificatesJson = certificatesJson,
			Bonus = "季度绩效待发放",
			LifecycleStatus = "已入职",
			CreatedAtUtc = SeedTimestamp(2026, 4, 15, 11, 0),
			ActivatedAtUtc = SeedTimestamp(2026, 4, 15, 11, 15),
			OnboardingNote = "已完成护理技能复核。",
		},
		new StaffMemberEntity
		{
			StaffId = "STF-PD-002",
			TenantId = TenantId,
			Name = "王照护",
			Role = "照护师",
			Department = "照护组",
			OrganizationId = "ORG-PD-01",
			OrganizationName = "浦东康养中心",
			EmploymentSource = "第三方合作",
			PartnerAgencyId = "PARTNER-ANXIN",
			PartnerAgencyName = "安心劳务",
			PartnerAffiliationRole = "外协护理",
			Phone = "13800138022",
			Status = "在职",
			Gender = "男",
			Email = "wang.support@nursing.local",
			Age = 33,
			Performance = 88,
			Attendance = 95,
			Satisfaction = 91,
			HireDate = "2025-10-15",
			ScheduleJson = defaultScheduleJson,
			CertificatesJson = JsonSerializer.Serialize(new[] { "养老护理员证" }),
			Bonus = "合作机构结算中",
			LifecycleStatus = "已入职",
			CreatedAtUtc = SeedTimestamp(2026, 4, 15, 11, 5),
			ActivatedAtUtc = SeedTimestamp(2026, 4, 15, 11, 20),
			OnboardingNote = "已完成外协准入复核。",
		},
		new StaffMemberEntity
		{
			StaffId = "STF-JA-001",
			TenantId = TenantId,
			Name = "赵班长",
			Role = "班组长",
			Department = "护理部",
			OrganizationId = "ORG-JA-01",
			OrganizationName = "静安护理院",
			EmploymentSource = "自有团队",
			Phone = "13800138023",
			Status = "在职",
			Gender = "女",
			Email = "zhao.lead@nursing.local",
			Age = 37,
			Performance = 95,
			Attendance = 98,
			Satisfaction = 96,
			HireDate = "2024-12-01",
			ScheduleJson = defaultScheduleJson,
			CertificatesJson = JsonSerializer.Serialize(new[] { "养老护理员证", "班组长培训证" }),
			Bonus = "季度绩效待发放",
			LifecycleStatus = "已入职",
			CreatedAtUtc = SeedTimestamp(2026, 4, 15, 11, 10),
			ActivatedAtUtc = SeedTimestamp(2026, 4, 15, 11, 25),
			OnboardingNote = "已完成院区排班授权。",
		},
	};

	var inserted = 0;
	var updated = 0;
	foreach (var staffMember in staffMembers)
	{
		var existing = await dbContext.StaffMembers.FindAsync(staffMember.StaffId);
		if (existing is null)
		{
			await dbContext.StaffMembers.AddAsync(staffMember);
			inserted++;
			continue;
		}

		existing.Name = staffMember.Name;
		existing.Role = staffMember.Role;
		existing.Department = staffMember.Department;
		existing.OrganizationId = staffMember.OrganizationId;
		existing.OrganizationName = staffMember.OrganizationName;
		existing.EmploymentSource = staffMember.EmploymentSource;
		existing.PartnerAgencyId = staffMember.PartnerAgencyId;
		existing.PartnerAgencyName = staffMember.PartnerAgencyName;
		existing.PartnerAffiliationRole = staffMember.PartnerAffiliationRole;
		existing.Phone = staffMember.Phone;
		existing.Status = staffMember.Status;
		existing.Gender = staffMember.Gender;
		existing.Email = staffMember.Email;
		existing.Age = staffMember.Age;
		existing.Performance = staffMember.Performance;
		existing.Attendance = staffMember.Attendance;
		existing.Satisfaction = staffMember.Satisfaction;
		existing.HireDate = staffMember.HireDate;
		existing.ScheduleJson = staffMember.ScheduleJson;
		existing.CertificatesJson = staffMember.CertificatesJson;
		existing.Bonus = staffMember.Bonus;
		existing.LifecycleStatus = staffMember.LifecycleStatus;
		existing.ActivatedAtUtc = staffMember.ActivatedAtUtc;
		existing.OnboardingNote = staffMember.OnboardingNote;
		updated++;
	}

	await dbContext.SaveChangesAsync();
	logger.LogInformation("Seeded staffing data. inserted={Inserted} updated={Updated}", inserted, updated);
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
	var activities = SeedActivities();
	var incidents = SeedIncidents();
	var equipment = SeedEquipment();
	var supplies = SeedSupplies();
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

	foreach (var activity in activities)
	{
		var existing = await dbContext.Activities.FindAsync(activity.ActivityId);
		if (existing is null)
		{
			await dbContext.Activities.AddAsync(activity);
			inserted++;
			continue;
		}

		existing.Name = activity.Name;
		existing.Category = activity.Category;
		existing.Date = activity.Date;
		existing.Time = activity.Time;
		existing.Duration = activity.Duration;
		existing.Participants = activity.Participants;
		existing.Capacity = activity.Capacity;
		existing.Location = activity.Location;
		existing.Status = activity.Status;
		existing.Teacher = activity.Teacher;
		existing.Description = activity.Description;
		existing.LifecycleStatus = activity.LifecycleStatus;
		existing.PublishedAtUtc = activity.PublishedAtUtc;
		existing.PublishNote = activity.PublishNote;
		updated++;
	}

	foreach (var incident in incidents)
	{
		var existing = await dbContext.Incidents.FindAsync(incident.IncidentId);
		if (existing is null)
		{
			await dbContext.Incidents.AddAsync(incident);
			inserted++;
			continue;
		}

		existing.Title = incident.Title;
		existing.Level = incident.Level;
		existing.ElderName = incident.ElderName;
		existing.Room = incident.Room;
		existing.Reporter = incident.Reporter;
		existing.ReporterRole = incident.ReporterRole;
		existing.OccurredAtUtc = incident.OccurredAtUtc;
		existing.Status = incident.Status;
		existing.Description = incident.Description;
		existing.HandlingJson = incident.HandlingJson;
		existing.NextStep = incident.NextStep;
		existing.AttachmentsJson = incident.AttachmentsJson;
		existing.AssignedAtUtc = incident.AssignedAtUtc;
		existing.ClosedAtUtc = incident.ClosedAtUtc;
		existing.StatusNote = incident.StatusNote;
		updated++;
	}

	foreach (var item in equipment)
	{
		var existing = await dbContext.Equipment.FindAsync(item.EquipmentId);
		if (existing is null)
		{
			await dbContext.Equipment.AddAsync(item);
			inserted++;
			continue;
		}

		existing.Name = item.Name;
		existing.Category = item.Category;
		existing.Model = item.Model;
		existing.SerialNumber = item.SerialNumber;
		existing.Location = item.Location;
		existing.Status = item.Status;
		existing.PurchaseDate = item.PurchaseDate;
		existing.MaintenanceDate = item.MaintenanceDate;
		existing.MaintenanceCycle = item.MaintenanceCycle;
		existing.OrganizationId = item.OrganizationId;
		existing.Remarks = item.Remarks;
		existing.Room = item.Room;
		existing.Type = item.Type;
		existing.Signal = item.Signal;
		existing.Battery = item.Battery;
		existing.Uptime = item.Uptime;
		existing.MetricsHr = item.MetricsHr;
		existing.MetricsBp = item.MetricsBp;
		existing.MetricsTemp = item.MetricsTemp;
		existing.MetricsSpo2 = item.MetricsSpo2;
		existing.HistoryJson = item.HistoryJson;
		existing.LifecycleStatus = item.LifecycleStatus;
		existing.ActivatedAtUtc = item.ActivatedAtUtc;
		existing.AcceptanceNote = item.AcceptanceNote;
		updated++;
	}

	foreach (var item in supplies)
	{
		var existing = await dbContext.Supplies.FindAsync(item.SupplyId);
		if (existing is null)
		{
			await dbContext.Supplies.AddAsync(item);
			inserted++;
			continue;
		}

		existing.Name = item.Name;
		existing.Category = item.Category;
		existing.Unit = item.Unit;
		existing.Stock = item.Stock;
		existing.MinStock = item.MinStock;
		existing.Price = item.Price;
		existing.Supplier = item.Supplier;
		existing.Contact = item.Contact;
		existing.LastPurchase = item.LastPurchase;
		existing.Status = item.Status;
		existing.LifecycleStatus = item.LifecycleStatus;
		existing.HistoryJson = item.HistoryJson;
		existing.ActivatedAtUtc = item.ActivatedAtUtc;
		existing.IntakeNote = item.IntakeNote;
		existing.LastIntakeQuantity = item.LastIntakeQuantity;
		updated++;
	}

	await dbContext.SaveChangesAsync();
	logger.LogInformation("Seeded operations data. inserted={Inserted} updated={Updated}", inserted, updated);
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

static ActivityEntity[] SeedActivities()
{
	return
	[
		new ActivityEntity
		{
			ActivityId = "ACT-1001",
			TenantId = TenantId,
			Name = "晨间关节舒展",
			Category = "康复活动",
			Date = "2026-04-14",
			Time = "09:30",
			Duration = 45,
			Participants = 12,
			Capacity = 18,
			Location = "康复大厅",
			Status = "报名中",
			Teacher = "李康复",
			Description = "面向晨间高频康复需求老人，侧重关节唤醒与步态稳定训练。",
			LifecycleStatus = "已发布",
			CreatedAtUtc = SeedTimestamp(2026, 4, 12, 15, 0),
			PublishedAtUtc = SeedTimestamp(2026, 4, 12, 16, 0),
			PublishNote = "已同步到晨间活动看板。",
		},
		new ActivityEntity
		{
			ActivityId = "ACT-1002",
			TenantId = TenantId,
			Name = "长者生日会",
			Category = "社交活动",
			Date = "2026-04-15",
			Time = "15:00",
			Duration = 60,
			Participants = 8,
			Capacity = 20,
			Location = "多功能厅",
			Status = "待发布",
			Teacher = "社工王芳",
			Description = "为本周生日长者准备的集体生日活动，含互动与家属连线。",
			LifecycleStatus = "待发布",
			CreatedAtUtc = SeedTimestamp(2026, 4, 13, 9, 20),
			PublishNote = "等待运营确认家属参会名单。",
		},
		new ActivityEntity
		{
			ActivityId = "ACT-1003",
			TenantId = TenantId,
			Name = "晚间音乐放松",
			Category = "情绪支持",
			Date = "2026-04-14",
			Time = "19:00",
			Duration = 30,
			Participants = 16,
			Capacity = 16,
			Location = "A 栋公共客厅",
			Status = "已完成",
			Teacher = "志愿者陈乐",
			Description = "针对晚间焦虑老人安排的轻音乐陪伴与呼吸放松活动。",
			LifecycleStatus = "已发布",
			CreatedAtUtc = SeedTimestamp(2026, 4, 11, 17, 0),
			PublishedAtUtc = SeedTimestamp(2026, 4, 11, 18, 0),
			PublishNote = "活动已归档，可用于周报复盘。",
		},
	];
}

static IncidentEntity[] SeedIncidents()
{
	return
	[
		new IncidentEntity
		{
			IncidentId = "INC-1001",
			TenantId = TenantId,
			Title = "夜间离床后轻微跌倒",
			Level = "严重",
			ElderName = "刘秀兰",
			Room = "B-0806",
			Reporter = "赵静",
			ReporterRole = "夜班护士",
			OccurredAtUtc = SeedTimestamp(2026, 4, 13, 2, 16),
			Status = "处理中",
			Description = "老人夜间自行离床，卫生间门口出现轻微跌倒，无明显出血。",
			HandlingJson = JsonSerializer.Serialize(new[] { "已完成首轮生命体征复测。", "值班主管已通知康复师晨间复查。" }),
			NextStep = "等待晨间复查结果并决定是否升级医生会诊。",
			AttachmentsJson = JsonSerializer.Serialize(new[] { "跌倒现场照片", "生命体征复测记录" }),
			CreatedAtUtc = SeedTimestamp(2026, 4, 13, 2, 25),
			AssignedAtUtc = SeedTimestamp(2026, 4, 13, 2, 32),
			StatusNote = "已进入持续观察。",
		},
		new IncidentEntity
		{
			IncidentId = "INC-1002",
			TenantId = TenantId,
			Title = "配药标签打印错误",
			Level = "一般",
			ElderName = null,
			Room = "药房",
			Reporter = "林倩",
			ReporterRole = "药房值守",
			OccurredAtUtc = SeedTimestamp(2026, 4, 13, 10, 8),
			Status = "待分派",
			Description = "午间批量打印配药标签时出现床号错位，已暂停出药。",
			HandlingJson = JsonSerializer.Serialize(new[] { "已冻结当前批次打印任务。" }),
			NextStep = "分派信息组排查打印模板配置。",
			AttachmentsJson = JsonSerializer.Serialize(new[] { "错位标签样张" }),
			CreatedAtUtc = SeedTimestamp(2026, 4, 13, 10, 12),
			StatusNote = "等待主管指派责任人。",
		},
		new IncidentEntity
		{
			IncidentId = "INC-1003",
			TenantId = TenantId,
			Title = "呼叫器误触发复盘完成",
			Level = "提示",
			ElderName = "王建国",
			Room = "A-1203",
			Reporter = "孙晨",
			ReporterRole = "值班主管",
			OccurredAtUtc = SeedTimestamp(2026, 4, 12, 21, 5),
			Status = "已结案",
			Description = "床旁呼叫器因充电线缠绕误触发，未造成伤害。",
			HandlingJson = JsonSerializer.Serialize(new[] { "已确认无人员伤害。", "已调整充电线固定方式并完成复盘。" }),
			NextStep = "纳入周例会提醒。",
			AttachmentsJson = JsonSerializer.Serialize(Array.Empty<string>()),
			CreatedAtUtc = SeedTimestamp(2026, 4, 12, 21, 10),
			AssignedAtUtc = SeedTimestamp(2026, 4, 12, 21, 15),
			ClosedAtUtc = SeedTimestamp(2026, 4, 12, 22, 0),
			StatusNote = "已归档。",
		},
	];
}

static EquipmentEntity[] SeedEquipment()
{
	return
	[
		new EquipmentEntity
		{
			EquipmentId = "EQ-1001",
			TenantId = TenantId,
			Name = "生命体征监护主机",
			Category = "监护设备",
			Model = "VM-900",
			SerialNumber = "VM900-2026-001",
			Location = "A-1203",
			Status = "正常",
			PurchaseDate = "2026-03-18",
			MaintenanceDate = "2026-09-18",
			MaintenanceCycle = 6,
			OrganizationId = "ORG-001",
			Remarks = "接入住区生命体征联动。",
			Room = "A-1203",
			Type = "监护设备",
			Signal = 94,
			Battery = 83,
			Uptime = 118,
			MetricsHr = 76,
			MetricsBp = "126/82",
			MetricsTemp = 36.6,
			MetricsSpo2 = 97,
			HistoryJson = JsonSerializer.Serialize(new[]
			{
				new { Time = "15:00", Hr = 74, Spo2 = 98, Note = "设备巡检通过。" },
				new { Time = "16:00", Hr = 76, Spo2 = 97, Note = "网络信号稳定。" },
			}),
			LifecycleStatus = "已入册",
			CreatedAtUtc = SeedTimestamp(2026, 3, 18, 9, 0),
			ActivatedAtUtc = SeedTimestamp(2026, 3, 18, 10, 0),
			AcceptanceNote = "已验收并接入日常巡检。",
		},
		new EquipmentEntity
		{
			EquipmentId = "EQ-1002",
			TenantId = TenantId,
			Name = "离床传感器",
			Category = "感知设备",
			Model = "BS-220",
			SerialNumber = "BS220-2026-008",
			Location = "B-0806",
			Status = "待维修",
			PurchaseDate = "2026-02-20",
			MaintenanceDate = "2026-05-20",
			MaintenanceCycle = 3,
			OrganizationId = "ORG-001",
			Remarks = "夜间误报码率偏高。",
			Room = "B-0806",
			Type = "感知设备",
			Signal = 68,
			Battery = 32,
			Uptime = 206,
			MetricsHr = 0,
			MetricsBp = "--",
			MetricsTemp = 0,
			MetricsSpo2 = 0,
			HistoryJson = JsonSerializer.Serialize(new[]
			{
				new { Time = "14:00", Hr = 0, Spo2 = 0, Note = "上报间歇性离线。" },
				new { Time = "16:00", Hr = 0, Spo2 = 0, Note = "已提交维修工单。" },
			}),
			LifecycleStatus = "已入册",
			CreatedAtUtc = SeedTimestamp(2026, 2, 20, 11, 0),
			ActivatedAtUtc = SeedTimestamp(2026, 2, 20, 14, 0),
			AcceptanceNote = "已转设备组跟进。",
		},
		new EquipmentEntity
		{
			EquipmentId = "EQ-1003",
			TenantId = TenantId,
			Name = "便携血压仪",
			Category = "护理工具",
			Model = "BP-530",
			SerialNumber = "BP530-2026-015",
			Location = "护理站仓库",
			Status = "正常",
			PurchaseDate = "2026-04-12",
			MaintenanceDate = "2026-10-12",
			MaintenanceCycle = 6,
			OrganizationId = "ORG-002",
			Remarks = "新到设备待分配楼层。",
			Room = "护理站仓库",
			Type = "护理工具",
			Signal = 100,
			Battery = 100,
			Uptime = 2,
			MetricsHr = 0,
			MetricsBp = "--",
			MetricsTemp = 0,
			MetricsSpo2 = 0,
			HistoryJson = JsonSerializer.Serialize(new[]
			{
				new { Time = "16:00", Hr = 0, Spo2 = 0, Note = "新设备录入，等待验收。" },
			}),
			LifecycleStatus = "待验收",
			CreatedAtUtc = SeedTimestamp(2026, 4, 12, 16, 20),
			AcceptanceNote = "等待资产管理员确认标签。",
		},
	];
}

static SupplyEntity[] SeedSupplies()
{
	return
	[
		new SupplyEntity
		{
			SupplyId = "SUP-1001",
			TenantId = TenantId,
			Name = "一次性手套",
			Category = "护理耗材",
			Unit = "盒",
			Stock = 48,
			MinStock = 20,
			Price = "¥26",
			Supplier = "安护医疗",
			Contact = "400-100-2001",
			LastPurchase = "2026-04-12",
			Status = "正常",
			LifecycleStatus = "已入库",
			HistoryJson = JsonSerializer.Serialize(new[]
			{
				new { Date = "2026-04-12", In = 20, Out = 0, Balance = 48 },
				new { Date = "2026-04-10", In = 0, Out = 8, Balance = 28 },
			}),
			CreatedAtUtc = SeedTimestamp(2026, 4, 1, 9, 30),
			ActivatedAtUtc = SeedTimestamp(2026, 4, 12, 10, 0),
			IntakeNote = "已完成抽检并上架。",
			LastIntakeQuantity = 20,
		},
		new SupplyEntity
		{
			SupplyId = "SUP-1002",
			TenantId = TenantId,
			Name = "营养流食",
			Category = "营养物资",
			Unit = "箱",
			Stock = 6,
			MinStock = 10,
			Price = "¥180",
			Supplier = "康健营养",
			Contact = "400-100-2002",
			LastPurchase = "2026-04-11",
			Status = "库存不足",
			LifecycleStatus = "已入库",
			HistoryJson = JsonSerializer.Serialize(new[]
			{
				new { Date = "2026-04-11", In = 8, Out = 0, Balance = 6 },
				new { Date = "2026-04-09", In = 0, Out = 5, Balance = 3 },
			}),
			CreatedAtUtc = SeedTimestamp(2026, 4, 2, 11, 0),
			ActivatedAtUtc = SeedTimestamp(2026, 4, 11, 13, 0),
			IntakeNote = "已提醒采购补货。",
			LastIntakeQuantity = 8,
		},
		new SupplyEntity
		{
			SupplyId = "SUP-1003",
			TenantId = TenantId,
			Name = "导尿护理包",
			Category = "护理耗材",
			Unit = "包",
			Stock = 15,
			MinStock = 12,
			Price = "¥48",
			Supplier = "安护医疗",
			Contact = "400-100-2001",
			LastPurchase = "2026-04-13",
			Status = "待上架",
			LifecycleStatus = "待上架",
			HistoryJson = JsonSerializer.Serialize(new[]
			{
				new { Date = "2026-04-13", In = 15, Out = 0, Balance = 15 },
			}),
			CreatedAtUtc = SeedTimestamp(2026, 4, 13, 15, 40),
			IntakeNote = "等待仓储复核批次标签。",
			LastIntakeQuantity = 15,
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