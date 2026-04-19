using NursingBackend.Bff.Admin;
using NursingBackend.BuildingBlocks.Contracts;
using NursingBackend.Services.Organization;
using NursingBackend.Services.Rooms;
using NursingBackend.Services.Staffing;

namespace NursingBackend.ArchitectureTests;

public class AdminLiveSlicePolicyTests
{
	[Fact]
	public void Organization_validation_requires_valid_contact_numbers()
	{
		var request = new OrganizationCreateRequest(
			Name: "浦东康养中心",
			Address: "浦东新区康桥路 88 号",
			Phone: "021-88",
			Manager: "张院长",
			ManagerPhone: "1380013800",
			Description: "测试机构");

		Assert.Equal("机构联系电话格式无效。", OrganizationServicePolicy.ValidateCreateRequest(request));

		var fixedPhone = request with { Phone = "021-88886666" };
		Assert.Equal("负责人电话至少填写 11 位有效手机号。", OrganizationServicePolicy.ValidateCreateRequest(fixedPhone));
	}

	[Fact]
	public void Room_validation_enforces_floor_and_capacity_bounds()
	{
		var request = new AdminRoomCreateRequest(
			RoomId: "R501",
			Name: "501 房",
			Floor: 0,
			Type: "双人间",
			Capacity: 2,
			OrganizationId: "ORG-1",
			OrganizationName: "浦东康养中心",
			Facilities: ["独立卫浴"]);

		Assert.Equal("房间楼层需在 1 到 20 之间。", RoomServicePolicy.ValidateCreateRequest(request));
		Assert.Equal("房间床位数需在 1 到 8 之间。", RoomServicePolicy.ValidateCreateRequest(request with { Floor = 5, Capacity = 9 }));
	}

	[Fact]
	public void Staffing_validation_requires_consistent_organization_and_partner_data()
	{
		var request = new AdminStaffCreateRequest(
			Name: "李护理",
			Role: "护理员",
			Department: "护理部",
			OrganizationId: "ORG-1",
			OrganizationName: null,
			EmploymentSource: "自有团队",
			PartnerAgencyId: null,
			PartnerAgencyName: null,
			PartnerAffiliationRole: null,
			Phone: "13800138000",
			Gender: "女",
			Email: "nurse@example.com",
			Age: 28,
			HireDate: "2026-04-17");

		Assert.Equal("机构归属需要同时填写机构 id 和机构名称。", StaffingServicePolicy.ValidateCreateRequest(request));
		Assert.Equal(
			"第三方合作人员必须填写合作机构名称。",
			StaffingServicePolicy.ValidateCreateRequest(request with
			{
				OrganizationName = "浦东康养中心",
				EmploymentSource = "第三方合作",
				PartnerAgencyName = null,
			}));
	}

	[Fact]
	public void Staffing_helpers_fall_back_to_default_schedule_for_invalid_json()
	{
		var schedule = StaffingServicePolicy.DeserializeSchedule("not-json");

		Assert.Equal(7, schedule.Count);
		Assert.All(schedule, item => Assert.Equal("待排班", item.Shift));
	}

	[Fact]
	public void Room_aggregation_projects_beds_and_full_status_from_live_elders()
	{
		var room = new AdminRoomRecordResponse(
			RoomId: "R501",
			TenantId: "tenant-demo",
			Name: "501 房",
			Floor: 5,
			FloorName: "5楼",
			Type: "双人间",
			Capacity: 2,
			Occupied: 0,
			Status: "可入住",
			OrganizationId: "ORG-1",
			OrganizationName: "浦东康养中心",
			Facilities: ["独立卫浴"],
			CleanStatus: "已清洁",
			LastClean: "2026-04-16 07:00",
			NextClean: "2026-04-17 07:00",
			LifecycleStatus: "已启用",
			CreatedAt: "2026-04-16",
			ActivatedAt: "2026-04-16 09:00",
			ActivationNote: null,
			BedsInfo: []);

		var elders = new[]
		{
			new ElderListItemResponse("ELD-2", "tenant-demo", "王秀兰", 81, "女", "一级护理", "R501", "已入住", "李梅", DateTimeOffset.Parse("2026-04-17T09:00:00+08:00")),
			new ElderListItemResponse("ELD-1", "tenant-demo", "陈玉芳", 79, "女", "二级护理", "r501", "已入住", "陈立", DateTimeOffset.Parse("2026-04-16T09:00:00+08:00")),
			new ElderListItemResponse("ELD-9", "tenant-demo", "赵阿姨", 88, "女", "三级护理", "R501", "已入住", "赵伟", null),
		};

		var merged = AdminBffAggregationPolicy.MergeRoomRecord(room, elders);

		Assert.Equal(2, merged.Occupied);
		Assert.Equal("已满", merged.Status);
		Assert.Equal("陈玉芳", Assert.IsType<AdminRoomBedOccupantResponse>(merged.BedsInfo[0].Elder).Name);
		Assert.Equal("王秀兰", Assert.IsType<AdminRoomBedOccupantResponse>(merged.BedsInfo[1].Elder).Name);
	}

	[Fact]
	public void Organization_aggregation_matches_by_id_or_name()
	{
		var organization = new OrganizationRecordResponse(
			OrganizationId: "ORG-1",
			TenantId: "tenant-demo",
			Name: "浦东康养中心",
			Address: "浦东新区康桥路 88 号",
			Phone: "021-88886666",
			Status: "运营中",
			EstablishedDate: "2026-04-16",
			Manager: "张院长",
			ManagerPhone: "13800138000",
			Description: "测试机构",
			LifecycleStatus: "已启用",
			CreatedAt: "2026-04-16",
			ActivatedAt: "2026-04-16 09:00",
			ActivationNote: null);

		var rooms = new[]
		{
			new AdminRoomRecordResponse("R501", "tenant-demo", "501 房", 5, "5楼", "双人间", 2, 2, "已满", "ORG-1", "浦东康养中心", [], "已清洁", "昨天", "今天", "已启用", "2026-04-16", null, null, []),
			new AdminRoomRecordResponse("R502", "tenant-demo", "502 房", 5, "5楼", "单人间", 1, 0, "可入住", null, "浦东康养中心", [], "已清洁", "昨天", "今天", "已启用", "2026-04-16", null, null, []),
			new AdminRoomRecordResponse("R601", "tenant-demo", "601 房", 6, "6楼", "单人间", 1, 1, "已满", "ORG-9", "其他机构", [], "已清洁", "昨天", "今天", "已启用", "2026-04-16", null, null, []),
		};

		var staff = new[]
		{
			new AdminStaffRecordResponse("STF-1", "tenant-demo", "李护理", "护理员", "护理部", "ORG-1", "浦东康养中心", "自有团队", null, null, null, "13800138001", "在职", "女", "n1@example.com", 28, 0, 0, 0, "2026-04-16", [], [], "待核定", "已入职", "2026-04-16T09:00:00Z", null, null),
			new AdminStaffRecordResponse("STF-2", "tenant-demo", "王照护", "照护师", "护理部", null, "浦东康养中心", "第三方合作", null, "安心劳务", null, "13800138002", "在职", "女", "n2@example.com", 31, 0, 0, 0, "2026-04-16", [], [], "待核定", "已入职", "2026-04-16T09:00:00Z", null, null),
			new AdminStaffRecordResponse("STF-3", "tenant-demo", "赵后勤", "后勤", "后勤部", "ORG-9", "其他机构", "自有团队", null, null, null, "13800138003", "在职", "男", "n3@example.com", 35, 0, 0, 0, "2026-04-16", [], [], "待核定", "已入职", "2026-04-16T09:00:00Z", null, null),
		};

		var summary = AdminBffAggregationPolicy.MergeOrganizationSummary(organization, rooms, staff);

		Assert.Equal(3, summary.TotalBeds);
		Assert.Equal(2, summary.OccupiedBeds);
		Assert.Equal(1, summary.AvailableBeds);
		Assert.Equal(2, summary.RoomCount);
		Assert.Equal(2, summary.StaffCount);
		Assert.Equal("live", summary.StaffIntegrationStatus);
	}

	[Fact]
	public void Assessment_helpers_dedupe_alerts_and_build_template_code()
	{
		var request = new AdminAssessmentCaseCreateRequest(
			ElderName: "陈玉芳",
			Age: 79,
			Gender: "女",
			Phone: "13800138003",
			EmergencyContact: "陈立 13800138004",
			RoomNumber: "R501",
			RequestedCareLevel: "一级护理",
			ChronicConditions: "高血压",
			MedicationSummary: "缬沙坦",
			AllergySummary: "高血压",
			AdlScore: 68,
			CognitiveLevel: "轻度认知下降",
			RiskNotes: " 夜间跌倒风险 ",
			EntrustmentType: "政府委托",
			EntrustmentOrganization: "民政局",
			MonthlySubsidy: 1200,
			ServiceItems: ["助浴"],
			ServiceNotes: null,
			SourceType: "manual-entry",
			SourceLabel: "人工建档",
			SourceDocumentNames: ["assessment.pdf"],
			SourceSummary: null);

		var alerts = AdminBffAggregationPolicy.BuildAssessmentMedicalAlerts(request);
		var recommendation = AdminBffAggregationPolicy.BuildAssessmentAiRecommendation(
			request,
			new AiAdmissionAssessmentResponse(
				RecommendedCareLevel: "一级护理",
				AssessmentSummary: "建议加强夜间巡视。",
				RiskFactors: ["夜间跌倒", "高血压"],
				Recommendations: ["增加巡视", "睡前复测血压"]));

		Assert.Equal(["高血压", "夜间跌倒风险"], alerts);
		Assert.Equal(84, recommendation.Confidence);
		Assert.Equal(76, recommendation.AssessmentScore);
		Assert.Equal("ASSESS-L3-HIGH", recommendation.PlanTemplateCode);
	}
}