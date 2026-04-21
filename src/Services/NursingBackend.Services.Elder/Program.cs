using Microsoft.EntityFrameworkCore;
using NursingBackend.BuildingBlocks.Context;
using NursingBackend.BuildingBlocks.Contracts;
using NursingBackend.BuildingBlocks.Entities;
using NursingBackend.BuildingBlocks.Hosting;
using NursingBackend.BuildingBlocks.Persistence;
using NursingBackend.Services.Elder;

var builder = WebApplication.CreateBuilder(args);
builder.AddPlatformDefaults();
builder.Services.AddDbContext<ElderDbContext>(options =>
	options.UseNpgsql(PostgresConnectionStrings.Resolve(builder.Configuration, "ElderPostgres", "nursing_elder")));

var app = builder.Build();
app.MapPlatformEndpoints(new PlatformServiceDescriptor(
	ServiceName: "elder-service",
	ServiceType: "domain-service",
	BoundedContext: "elder-management",
	Consumers: ["admin-bff", "family-bff", "care-service", "visit-service", "billing-service"],
	Capabilities: ["elder-registry", "admission-lifecycle", "family-binding", "resident-profile", "assessment-case"]));

app.MapGet("/api/elders", async (HttpContext context, ElderDbContext dbContext, string? name, string? status, string? careLevel, int page = 1, int pageSize = 20) =>
{
	var requestContext = context.GetPlatformRequestContext();
	if (requestContext is null || string.IsNullOrWhiteSpace(requestContext.TenantId))
	{
		return Results.Problem(title: "缺少租户上下文。", statusCode: StatusCodes.Status400BadRequest);
	}

	var query = dbContext.Elders.Where(e => e.TenantId == requestContext.TenantId);

	if (!string.IsNullOrWhiteSpace(name))
		query = query.Where(e => e.ElderName.Contains(name));
	if (!string.IsNullOrWhiteSpace(status))
		query = query.Where(e => e.AdmissionStatus == status);
	if (!string.IsNullOrWhiteSpace(careLevel))
		query = query.Where(e => e.CareLevel == careLevel);

	var total = await query.CountAsync();

	var items = await query
		.OrderBy(e => e.ElderName)
		.Skip((page - 1) * pageSize)
		.Take(pageSize)
		.Select(e => new ElderListItemResponse(
			ElderId: e.ElderId,
			TenantId: e.TenantId,
			ElderName: e.ElderName,
			Age: e.Age,
			Gender: e.Gender,
			CareLevel: e.CareLevel,
			RoomNumber: e.RoomNumber,
			AdmissionStatus: e.AdmissionStatus,
			FamilyContactName: e.FamilyContactName,
			AdmissionCreatedAtUtc: dbContext.Admissions
				.Where(admission => admission.TenantId == e.TenantId && admission.ElderId == e.ElderId)
				.OrderByDescending(admission => admission.CreatedAtUtc)
				.Select(admission => (DateTimeOffset?)admission.CreatedAtUtc)
				.FirstOrDefault()))
		.ToListAsync();

	return Results.Ok(new ElderListResponse(Items: items, Total: total, Page: page, PageSize: pageSize));
}).RequireAuthorization();

app.MapPost("/api/elders/admissions", async (HttpContext context, AdmissionCreateRequest request, ElderDbContext dbContext) =>
{
	var requestContext = context.GetPlatformRequestContext();
	if (requestContext is null || string.IsNullOrWhiteSpace(requestContext.TenantId))
	{
		return Results.Problem(title: "缺少租户上下文。", statusCode: StatusCodes.Status400BadRequest);
	}

	var admissionId = $"ADM-{DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}";
	var elderId = $"ELD-{DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}";
	var createdAtUtc = DateTimeOffset.UtcNow;

	var admission = new AdmissionRecordEntity
	{
		AdmissionId = admissionId,
		TenantId = requestContext.TenantId,
		ElderId = elderId,
		AdmissionReference = request.AdmissionReference,
		Status = "AdmissionReviewed",
		CareLevel = request.CareLevel,
		RoomNumber = request.RoomNumber,
		CreatedAtUtc = createdAtUtc,
		AssessmentStatus = string.Empty,
		RequestedCareLevel = string.Empty,
		Phone = string.Empty,
		EmergencyContact = string.Empty,
		ChronicConditions = string.Empty,
		MedicationSummary = string.Empty,
		AllergySummary = string.Empty,
		AdlScore = 0,
		CognitiveLevel = string.Empty,
		RiskNotes = string.Empty,
		SourceType = string.Empty,
		SourceDocumentNames = [],
		AiRecommendedCareLevel = string.Empty,
		AiReasonSummary = string.Empty,
		AiReasons = [],
		AiFocusTags = [],
		AiPlanTemplateCode = string.Empty,
	};

	var elder = new ElderProfileEntity
	{
		ElderId = elderId,
		TenantId = requestContext.TenantId,
		ElderName = request.ElderName,
		Age = request.Age,
		Gender = request.Gender,
		CareLevel = request.CareLevel,
		RoomNumber = request.RoomNumber,
		IdentityCard = NormalizeOptionalText(request.IdentityCard),
		BirthDate = NormalizeOptionalText(request.BirthDate),
		ElderPhone = NormalizeOptionalText(request.ElderPhone),
		FamilyContactName = request.FamilyContactName,
		FamilyContactPhone = request.FamilyContactPhone,
		AdlScore = NormalizeAdlScore(request.AdlScore),
		CognitiveLevel = NormalizeOptionalText(request.CognitiveLevel),
		MedicalAlerts = NormalizeStringList(request.MedicalAlerts),
		AdmissionStatus = admission.Status,
		EntrustmentType = NormalizeOptionalText(request.EntrustmentType),
		EntrustmentOrganization = NormalizeOptionalText(request.EntrustmentOrganization),
		MonthlySubsidy = NormalizeMonthlySubsidy(request.MonthlySubsidy),
		ServiceItems = NormalizeStringList(request.ServiceItems),
		ServiceNotes = NormalizeOptionalText(request.ServiceNotes),
	};

	dbContext.Admissions.Add(admission);
	dbContext.Elders.Add(elder);
	dbContext.OutboxMessages.Add(new OutboxMessageEntity
	{
		OutboxMessageId = $"OUT-{admissionId}",
		TenantId = requestContext.TenantId,
		AggregateType = "Admission",
		AggregateId = admissionId,
		EventType = "AdmissionReviewed",
		PayloadJson = System.Text.Json.JsonSerializer.Serialize(new { admissionId, elderId, request.ElderName, request.CareLevel }),
		CreatedAtUtc = createdAtUtc,
	});
	await dbContext.SaveChangesAsync();

	return Results.Ok(new AdmissionRecordResponse(
		AdmissionId: admission.AdmissionId,
		ElderId: admission.ElderId,
		TenantId: admission.TenantId,
		ElderName: elder.ElderName,
		CareLevel: admission.CareLevel,
		RoomNumber: admission.RoomNumber,
		Status: admission.Status,
		CreatedAtUtc: admission.CreatedAtUtc));
}).RequireAuthorization();

app.MapGet("/api/elders/assessments", async (HttpContext context, ElderDbContext dbContext, string? keyword, string? status, string? sourceType, string? scene, int page = 1, int pageSize = 20) =>
{
	var requestContext = context.GetPlatformRequestContext();
	if (requestContext is null || string.IsNullOrWhiteSpace(requestContext.TenantId))
	{
		return Results.Problem(title: "缺少租户上下文。", statusCode: StatusCodes.Status400BadRequest);
	}

	var query =
		from admission in dbContext.Admissions
		join elder in dbContext.Elders on new { admission.ElderId, admission.TenantId } equals new { elder.ElderId, elder.TenantId }
		where admission.TenantId == requestContext.TenantId && !string.IsNullOrWhiteSpace(admission.RequestedCareLevel)
		select new { admission, elder };

	if (!string.IsNullOrWhiteSpace(status))
	{
		query = query.Where(item => item.admission.AssessmentStatus == status);
	}

	if (!string.IsNullOrWhiteSpace(sourceType))
	{
		query = query.Where(item => item.admission.SourceType == sourceType);
	}

	if (string.Equals(scene, "home", StringComparison.OrdinalIgnoreCase))
	{
		query = query.Where(item => item.admission.SourceType == "document-import");
	}
	else if (string.Equals(scene, "institutional", StringComparison.OrdinalIgnoreCase))
	{
		query = query.Where(item => item.admission.SourceType == "manual-form");
	}

	if (!string.IsNullOrWhiteSpace(keyword))
	{
		query = query.Where(item =>
			item.admission.AdmissionId.Contains(keyword)
			|| item.elder.ElderName.Contains(keyword)
			|| item.admission.RoomNumber.Contains(keyword));
	}

	var total = await query.CountAsync();

	var items = await query
		.OrderByDescending(item => item.admission.CreatedAtUtc)
		.Skip((page - 1) * pageSize)
		.Take(pageSize)
		.ToListAsync();

	return Results.Ok(new AssessmentCaseListResponse(
		Items: items.Select(item => MapAssessmentCase(item.admission, item.elder)).ToArray(),
		Total: total,
		Page: page,
		PageSize: pageSize));
}).RequireAuthorization();

app.MapPost("/api/elders/assessments", async (HttpContext context, AssessmentCaseCreateRequest request, ElderDbContext dbContext) =>
{
	var requestContext = context.GetPlatformRequestContext();
	if (requestContext is null || string.IsNullOrWhiteSpace(requestContext.TenantId))
	{
		return Results.Problem(title: "缺少租户上下文。", statusCode: StatusCodes.Status400BadRequest);
	}

	if (string.IsNullOrWhiteSpace(request.ElderName)
		|| request.Age <= 0
		|| string.IsNullOrWhiteSpace(request.Gender)
		|| string.IsNullOrWhiteSpace(request.RoomNumber)
		|| string.IsNullOrWhiteSpace(request.RequestedCareLevel)
		|| request.AdlScore < 0
		|| string.IsNullOrWhiteSpace(request.CognitiveLevel))
	{
		return Results.Problem(title: "评定个案必填字段不完整。", statusCode: StatusCodes.Status400BadRequest);
	}

	var createdAtUtc = DateTimeOffset.UtcNow;
	var admissionId = $"ADM-{createdAtUtc.ToUnixTimeMilliseconds()}";
	var elderId = $"ELD-{createdAtUtc.ToUnixTimeMilliseconds()}";

	var admission = new AdmissionRecordEntity
	{
		AdmissionId = admissionId,
		TenantId = requestContext.TenantId,
		ElderId = elderId,
		AdmissionReference = admissionId,
		Status = "AdmissionReviewed",
		CareLevel = request.RequestedCareLevel.Trim(),
		RoomNumber = request.RoomNumber.Trim(),
		CreatedAtUtc = createdAtUtc,
		AssessmentStatus = "待人工确认",
		RequestedCareLevel = request.RequestedCareLevel.Trim(),
		Phone = request.Phone.Trim(),
		EmergencyContact = request.EmergencyContact.Trim(),
		ChronicConditions = request.ChronicConditions.Trim(),
		MedicationSummary = request.MedicationSummary.Trim(),
		AllergySummary = request.AllergySummary.Trim(),
		AdlScore = request.AdlScore,
		CognitiveLevel = request.CognitiveLevel.Trim(),
		RiskNotes = request.RiskNotes.Trim(),
		SourceType = NormalizeSourceType(request.SourceType),
		SourceLabel = NormalizeOptionalText(request.SourceLabel),
		SourceDocumentNames = NormalizeStringList(request.SourceDocumentNames),
		SourceSummary = NormalizeOptionalText(request.SourceSummary),
		AiRecommendedCareLevel = request.AiRecommendation.RecommendedLevel.Trim(),
		AiConfidence = request.AiRecommendation.Confidence,
		AiAssessmentScore = request.AiRecommendation.AssessmentScore,
		AiReasonSummary = request.AiRecommendation.ReasonSummary.Trim(),
		AiReasons = NormalizeStringList(request.AiRecommendation.Reasons),
		AiFocusTags = NormalizeStringList(request.AiRecommendation.FocusTags),
		AiPlanTemplateCode = request.AiRecommendation.PlanTemplateCode.Trim(),
	};

	var elder = new ElderProfileEntity
	{
		ElderId = elderId,
		TenantId = requestContext.TenantId,
		ElderName = request.ElderName.Trim(),
		Age = request.Age,
		Gender = request.Gender.Trim(),
		CareLevel = request.RequestedCareLevel.Trim(),
		RoomNumber = request.RoomNumber.Trim(),
		IdentityCard = null,
		BirthDate = null,
		ElderPhone = NormalizeOptionalText(request.Phone),
		FamilyContactName = request.EmergencyContact.Trim(),
		FamilyContactPhone = request.Phone.Trim(),
		AdlScore = NormalizeAdlScore(request.AdlScore),
		CognitiveLevel = NormalizeOptionalText(request.CognitiveLevel),
		MedicalAlerts = BuildMedicalAlerts(request),
		AdmissionStatus = "AdmissionReviewed",
		EntrustmentType = NormalizeOptionalText(request.EntrustmentType),
		EntrustmentOrganization = NormalizeOptionalText(request.EntrustmentOrganization),
		MonthlySubsidy = NormalizeMonthlySubsidy(request.MonthlySubsidy),
		ServiceItems = NormalizeStringList(request.ServiceItems),
		ServiceNotes = NormalizeOptionalText(request.ServiceNotes),
	};

	dbContext.Admissions.Add(admission);
	dbContext.Elders.Add(elder);
	dbContext.OutboxMessages.Add(new OutboxMessageEntity
	{
		OutboxMessageId = $"OUT-{admissionId}",
		TenantId = requestContext.TenantId,
		AggregateType = "AssessmentCase",
		AggregateId = admissionId,
		EventType = "AssessmentCaseCreated",
		PayloadJson = System.Text.Json.JsonSerializer.Serialize(new
		{
			assessmentId = admissionId,
			elderId,
			request.ElderName,
			request.RequestedCareLevel,
			admission.AssessmentStatus,
			admission.SourceType,
		}),
		CreatedAtUtc = createdAtUtc,
	});

	await dbContext.SaveChangesAsync();

	return Results.Ok(MapAssessmentCase(admission, elder));
}).RequireAuthorization();

app.MapPut("/api/elders/assessments/{assessmentId}/decision", async (HttpContext context, string assessmentId, AssessmentDecisionUpdateRequest request, ElderDbContext dbContext) =>
{
	var requestContext = context.GetPlatformRequestContext();
	if (requestContext is null || string.IsNullOrWhiteSpace(requestContext.TenantId))
	{
		return Results.Problem(title: "缺少租户上下文。", statusCode: StatusCodes.Status400BadRequest);
	}

	if (string.IsNullOrWhiteSpace(request.ConfirmedCareLevel) || string.IsNullOrWhiteSpace(request.ConfirmedBy))
	{
		return Results.Problem(title: "认定等级和认定人不能为空。", statusCode: StatusCodes.Status400BadRequest);
	}

	var admission = await dbContext.Admissions.FirstOrDefaultAsync(item => item.AdmissionId == assessmentId && item.TenantId == requestContext.TenantId);
	if (admission is null || string.IsNullOrWhiteSpace(admission.RequestedCareLevel))
	{
		return Results.Problem(title: $"评定个案 {assessmentId} 不存在。", statusCode: StatusCodes.Status404NotFound);
	}

	var elder = await dbContext.Elders.FirstOrDefaultAsync(item => item.ElderId == admission.ElderId && item.TenantId == requestContext.TenantId);
	if (elder is null)
	{
		return Results.Problem(title: $"评定个案 {assessmentId} 对应长者不存在。", statusCode: StatusCodes.Status404NotFound);
	}

	var confirmedAtUtc = DateTimeOffset.UtcNow;
	admission.ConfirmedCareLevel = request.ConfirmedCareLevel.Trim();
	admission.ReviewNote = NormalizeOptionalText(request.ReviewNote);
	admission.ConfirmedBy = request.ConfirmedBy.Trim();
	admission.ConfirmedAtUtc = confirmedAtUtc;
	admission.AssessmentStatus = "计划已生成";

	dbContext.OutboxMessages.Add(new OutboxMessageEntity
	{
		OutboxMessageId = $"OUT-{assessmentId}-DECISION-{confirmedAtUtc.ToUnixTimeMilliseconds()}",
		TenantId = requestContext.TenantId,
		AggregateType = "AssessmentCase",
		AggregateId = assessmentId,
		EventType = "AssessmentDecisionConfirmed",
		PayloadJson = System.Text.Json.JsonSerializer.Serialize(new
		{
			assessmentId,
			admission.ElderId,
			admission.ConfirmedCareLevel,
			admission.ConfirmedBy,
			admission.AssessmentStatus,
			confirmedAtUtc,
		}),
		CreatedAtUtc = confirmedAtUtc,
	});

	await dbContext.SaveChangesAsync();

	return Results.Ok(MapAssessmentCase(admission, elder));
}).RequireAuthorization();

app.MapPut("/api/elders/assessments/{assessmentId}/activate", async (HttpContext context, string assessmentId, ElderDbContext dbContext) =>
{
	var requestContext = context.GetPlatformRequestContext();
	if (requestContext is null || string.IsNullOrWhiteSpace(requestContext.TenantId))
	{
		return Results.Problem(title: "缺少租户上下文。", statusCode: StatusCodes.Status400BadRequest);
	}

	var admission = await dbContext.Admissions.FirstOrDefaultAsync(item => item.AdmissionId == assessmentId && item.TenantId == requestContext.TenantId);
	if (admission is null || string.IsNullOrWhiteSpace(admission.RequestedCareLevel))
	{
		return Results.Problem(title: $"评定个案 {assessmentId} 不存在。", statusCode: StatusCodes.Status404NotFound);
	}

	if (admission.AssessmentStatus != "计划已生成")
	{
		return Results.Problem(title: "当前个案尚未进入待生效状态。", statusCode: StatusCodes.Status400BadRequest);
	}

	var elder = await dbContext.Elders.FirstOrDefaultAsync(item => item.ElderId == admission.ElderId && item.TenantId == requestContext.TenantId);
	if (elder is null)
	{
		return Results.Problem(title: $"评定个案 {assessmentId} 对应长者不存在。", statusCode: StatusCodes.Status404NotFound);
	}

	var activatedAtUtc = DateTimeOffset.UtcNow;
	admission.AssessmentStatus = "已入住";
	admission.Status = "Active";
	elder.AdmissionStatus = "Active";
	elder.CareLevel = admission.ConfirmedCareLevel ?? admission.AiRecommendedCareLevel ?? admission.RequestedCareLevel;

	dbContext.OutboxMessages.Add(new OutboxMessageEntity
	{
		OutboxMessageId = $"OUT-{assessmentId}-ACTIVATE-{activatedAtUtc.ToUnixTimeMilliseconds()}",
		TenantId = requestContext.TenantId,
		AggregateType = "AssessmentCase",
		AggregateId = assessmentId,
		EventType = "AssessmentCaseActivated",
		PayloadJson = System.Text.Json.JsonSerializer.Serialize(new
		{
			assessmentId,
			admission.ElderId,
			admission.AssessmentStatus,
			admission.Status,
			elder.CareLevel,
			activatedAtUtc,
		}),
		CreatedAtUtc = activatedAtUtc,
	});

	await dbContext.SaveChangesAsync();

	return Results.Ok(MapAssessmentCase(admission, elder));
}).RequireAuthorization();

app.MapGet("/api/elders/{elderId}", async (string elderId, ElderDbContext dbContext) =>
{
	var elder = await dbContext.Elders.FirstOrDefaultAsync(item => item.ElderId == elderId);
	if (elder is null)
	{
		return Results.Problem(title: $"老人 {elderId} 不存在。", statusCode: StatusCodes.Status404NotFound);
	}

	return Results.Ok(MapProfileSummary(elder));
}).RequireAuthorization();

app.MapPut("/api/elders/{elderId}", async (HttpContext context, string elderId, ElderProfileUpdateRequest request, ElderDbContext dbContext) =>
{
	var requestContext = context.GetPlatformRequestContext();
	if (requestContext is null || string.IsNullOrWhiteSpace(requestContext.TenantId))
	{
		return Results.Problem(title: "缺少租户上下文。", statusCode: StatusCodes.Status400BadRequest);
	}

	if (string.IsNullOrWhiteSpace(request.CareLevel) || string.IsNullOrWhiteSpace(request.RoomNumber))
	{
		return Results.Problem(title: "护理等级和房间号不能为空。", statusCode: StatusCodes.Status400BadRequest);
	}

	var elder = await dbContext.Elders.FirstOrDefaultAsync(item => item.ElderId == elderId && item.TenantId == requestContext.TenantId);
	if (elder is null)
	{
		return Results.Problem(title: $"老人 {elderId} 不存在。", statusCode: StatusCodes.Status404NotFound);
	}

	elder.CareLevel = request.CareLevel.Trim();
	elder.RoomNumber = request.RoomNumber.Trim();
	if (request.Age is > 0)
	{
		elder.Age = request.Age.Value;
	}

	var normalizedGender = NormalizeOptionalText(request.Gender);
	if (!string.IsNullOrWhiteSpace(normalizedGender))
	{
		elder.Gender = normalizedGender;
	}

	elder.IdentityCard = NormalizeOptionalText(request.IdentityCard);
	elder.BirthDate = NormalizeOptionalText(request.BirthDate);
	elder.ElderPhone = NormalizeOptionalText(request.ElderPhone);
	elder.FamilyContactName = request.FamilyContactName.Trim();
	elder.FamilyContactPhone = request.FamilyContactPhone.Trim();
	elder.AdlScore = NormalizeAdlScore(request.AdlScore);
	elder.CognitiveLevel = NormalizeOptionalText(request.CognitiveLevel);
	elder.MedicalAlerts = NormalizeStringList(request.MedicalAlerts);
	elder.EntrustmentType = NormalizeOptionalText(request.EntrustmentType);
	elder.EntrustmentOrganization = NormalizeOptionalText(request.EntrustmentOrganization);
	elder.MonthlySubsidy = NormalizeMonthlySubsidy(request.MonthlySubsidy);
	elder.ServiceItems = NormalizeStringList(request.ServiceItems);
	elder.ServiceNotes = NormalizeOptionalText(request.ServiceNotes);

	var updatedAtUtc = DateTimeOffset.UtcNow;
	dbContext.OutboxMessages.Add(new OutboxMessageEntity
	{
		OutboxMessageId = $"OUT-ELDER-{elderId}-{updatedAtUtc.ToUnixTimeMilliseconds()}",
		TenantId = requestContext.TenantId,
		AggregateType = "Elder",
		AggregateId = elderId,
		EventType = "ElderProfileUpdated",
		PayloadJson = System.Text.Json.JsonSerializer.Serialize(new
		{
			elderId,
			elder.CareLevel,
			elder.RoomNumber,
			elder.EntrustmentType,
			elder.MonthlySubsidy,
			updatedAtUtc,
		}),
		CreatedAtUtc = updatedAtUtc,
	});

	await dbContext.SaveChangesAsync();

	return Results.Ok(MapProfileSummary(elder));
}).RequireAuthorization();

app.MapGet("/api/elders/face-enrollment", async (HttpContext context, ElderDbContext dbContext, string? keyword, string? status, int page = 1, int pageSize = 50) =>
{
	var requestContext = context.GetPlatformRequestContext();
	if (requestContext is null || string.IsNullOrWhiteSpace(requestContext.TenantId))
	{
		return Results.Problem(title: "缺少租户上下文。", statusCode: StatusCodes.Status400BadRequest);
	}

	var query = dbContext.Elders.Where(item => item.TenantId == requestContext.TenantId);
	if (!string.IsNullOrWhiteSpace(keyword))
	{
		query = query.Where(item => item.ElderId.Contains(keyword) || item.ElderName.Contains(keyword) || item.RoomNumber.Contains(keyword));
	}

	if (!string.IsNullOrWhiteSpace(status))
	{
		query = query.Where(item => item.FaceEnrollmentStatus == status);
	}

	var normalizedPage = Math.Max(1, page);
	var normalizedPageSize = Math.Clamp(pageSize, 1, 500);
	var total = await query.CountAsync();
	var items = await query
		.OrderByDescending(item => item.FaceLastUpdatedUtc ?? DateTimeOffset.MinValue)
		.ThenBy(item => item.ElderName)
		.Skip((normalizedPage - 1) * normalizedPageSize)
		.Take(normalizedPageSize)
		.ToListAsync();

	return Results.Ok(new ElderFaceEnrollmentListResponse(
		Items: items.Select(MapFaceEnrollment).ToArray(),
		Total: total,
		Page: normalizedPage,
		PageSize: normalizedPageSize));
}).RequireAuthorization();

app.MapPost("/api/elders/{elderId}/face-enrollment/start", async (HttpContext context, string elderId, ElderFaceEnrollmentUpdateRequest request, ElderDbContext dbContext) =>
{
	var requestContext = context.GetPlatformRequestContext();
	if (requestContext is null || string.IsNullOrWhiteSpace(requestContext.TenantId))
	{
		return Results.Problem(title: "缺少租户上下文。", statusCode: StatusCodes.Status400BadRequest);
	}

	if (string.IsNullOrWhiteSpace(request.Operator) || string.IsNullOrWhiteSpace(request.DeviceLabel))
	{
		return Results.Problem(title: "采集操作人和采集终端不能为空。", statusCode: StatusCodes.Status400BadRequest);
	}

	var elder = await dbContext.Elders.FirstOrDefaultAsync(item => item.ElderId == elderId && item.TenantId == requestContext.TenantId);
	if (elder is null)
	{
		return Results.Problem(title: $"老人 {elderId} 不存在。", statusCode: StatusCodes.Status404NotFound);
	}

	var steps = elder.FaceEnrollmentStatus == "采集中"
		? NormalizeStringList(elder.FaceCapturedSteps)
		: [];
	var qualityScore = QualityForFaceSteps(steps);
	var nextStatus = steps.Count >= 3 ? "待确认" : "采集中";
	var now = DateTimeOffset.UtcNow;

	elder.FaceEnrollmentStatus = nextStatus;
	elder.FaceCapturedSteps = steps;
	elder.FaceQualityScore = qualityScore;
	elder.FaceQualitySummary = SummarizeFaceQuality(nextStatus, steps, qualityScore, null);
	elder.FaceOperator = request.Operator.Trim();
	elder.FaceDeviceLabel = request.DeviceLabel.Trim();
	elder.FaceEntrySource = NormalizeOptionalText(request.EntrySource) ?? "face-page";
	elder.FaceLastUpdatedUtc = now;
	elder.FaceRetakeReason = null;

	dbContext.OutboxMessages.Add(new OutboxMessageEntity
	{
		OutboxMessageId = $"OUT-ELDER-FACE-START-{elderId}-{now.ToUnixTimeMilliseconds()}",
		TenantId = requestContext.TenantId,
		AggregateType = "ElderFaceEnrollment",
		AggregateId = elderId,
		EventType = "ElderFaceEnrollmentStarted",
		PayloadJson = System.Text.Json.JsonSerializer.Serialize(new
		{
			elderId,
			elder.FaceEnrollmentStatus,
			elder.FaceCapturedSteps,
			elder.FaceOperator,
			elder.FaceDeviceLabel,
			now,
		}),
		CreatedAtUtc = now,
	});

	await dbContext.SaveChangesAsync();
	return Results.Ok(MapFaceEnrollment(elder));
}).RequireAuthorization();

app.MapPost("/api/elders/{elderId}/face-enrollment/capture", async (HttpContext context, string elderId, ElderFaceCaptureRequest request, ElderDbContext dbContext) =>
{
	var requestContext = context.GetPlatformRequestContext();
	if (requestContext is null || string.IsNullOrWhiteSpace(requestContext.TenantId))
	{
		return Results.Problem(title: "缺少租户上下文。", statusCode: StatusCodes.Status400BadRequest);
	}

	if (string.IsNullOrWhiteSpace(request.Operator) || string.IsNullOrWhiteSpace(request.DeviceLabel))
	{
		return Results.Problem(title: "采集操作人和采集终端不能为空。", statusCode: StatusCodes.Status400BadRequest);
	}

	var step = NormalizeFaceCaptureStep(request.Step);
	if (step is null)
	{
		return Results.Problem(title: "人脸采集角度无效。", statusCode: StatusCodes.Status400BadRequest);
	}

	var elder = await dbContext.Elders.FirstOrDefaultAsync(item => item.ElderId == elderId && item.TenantId == requestContext.TenantId);
	if (elder is null)
	{
		return Results.Problem(title: $"老人 {elderId} 不存在。", statusCode: StatusCodes.Status404NotFound);
	}

	var steps = (elder.FaceEnrollmentStatus == "待录入" || elder.FaceEnrollmentStatus == "需重录")
		? []
		: NormalizeStringList(elder.FaceCapturedSteps);
	if (!steps.Contains(step, StringComparer.Ordinal))
	{
		steps.Add(step);
	}

	var qualityScore = QualityForFaceSteps(steps);
	var nextStatus = steps.Count >= 3 ? "待确认" : "采集中";
	var now = DateTimeOffset.UtcNow;

	elder.FaceEnrollmentStatus = nextStatus;
	elder.FaceCapturedSteps = steps;
	elder.FaceQualityScore = qualityScore;
	elder.FaceQualitySummary = SummarizeFaceQuality(nextStatus, steps, qualityScore, null);
	elder.FaceOperator = request.Operator.Trim();
	elder.FaceDeviceLabel = request.DeviceLabel.Trim();
	elder.FaceLastUpdatedUtc = now;
	elder.FaceRetakeReason = null;

	dbContext.OutboxMessages.Add(new OutboxMessageEntity
	{
		OutboxMessageId = $"OUT-ELDER-FACE-CAPTURE-{elderId}-{now.ToUnixTimeMilliseconds()}",
		TenantId = requestContext.TenantId,
		AggregateType = "ElderFaceEnrollment",
		AggregateId = elderId,
		EventType = "ElderFaceSampleCaptured",
		PayloadJson = System.Text.Json.JsonSerializer.Serialize(new
		{
			elderId,
			step,
			elder.FaceEnrollmentStatus,
			elder.FaceCapturedSteps,
			now,
		}),
		CreatedAtUtc = now,
	});

	await dbContext.SaveChangesAsync();
	return Results.Ok(MapFaceEnrollment(elder));
}).RequireAuthorization();

app.MapPost("/api/elders/{elderId}/face-enrollment/activate", async (HttpContext context, string elderId, ElderFaceActivationRequest request, ElderDbContext dbContext) =>
{
	var requestContext = context.GetPlatformRequestContext();
	if (requestContext is null || string.IsNullOrWhiteSpace(requestContext.TenantId))
	{
		return Results.Problem(title: "缺少租户上下文。", statusCode: StatusCodes.Status400BadRequest);
	}

	if (string.IsNullOrWhiteSpace(request.ActivationNote))
	{
		return Results.Problem(title: "激活备注不能为空。", statusCode: StatusCodes.Status400BadRequest);
	}

	var elder = await dbContext.Elders.FirstOrDefaultAsync(item => item.ElderId == elderId && item.TenantId == requestContext.TenantId);
	if (elder is null)
	{
		return Results.Problem(title: $"老人 {elderId} 不存在。", statusCode: StatusCodes.Status404NotFound);
	}

	var steps = NormalizeStringList(elder.FaceCapturedSteps);
	if (steps.Count < 3)
	{
		return Results.Problem(title: "请先补齐三个角度样本后再激活。", statusCode: StatusCodes.Status400BadRequest);
	}

	var now = DateTimeOffset.UtcNow;
	elder.FaceEnrollmentStatus = "已生效";
	elder.FaceCapturedSteps = steps;
	elder.FaceQualityScore = Math.Max(elder.FaceQualityScore, 92);
	elder.FaceQualitySummary = SummarizeFaceQuality("已生效", steps, elder.FaceQualityScore, null);
	elder.FaceActivationNote = request.ActivationNote.Trim();
	elder.FaceActivatedAtUtc = now;
	elder.FaceLastUpdatedUtc = now;
	elder.FaceRetakeReason = null;

	dbContext.OutboxMessages.Add(new OutboxMessageEntity
	{
		OutboxMessageId = $"OUT-ELDER-FACE-ACTIVATE-{elderId}-{now.ToUnixTimeMilliseconds()}",
		TenantId = requestContext.TenantId,
		AggregateType = "ElderFaceEnrollment",
		AggregateId = elderId,
		EventType = "ElderFaceEnrollmentActivated",
		PayloadJson = System.Text.Json.JsonSerializer.Serialize(new
		{
			elderId,
			elder.FaceEnrollmentStatus,
			elder.FaceQualityScore,
			elder.FaceActivatedAtUtc,
			now,
		}),
		CreatedAtUtc = now,
	});

	await dbContext.SaveChangesAsync();
	return Results.Ok(MapFaceEnrollment(elder));
}).RequireAuthorization();

app.MapPost("/api/elders/{elderId}/face-enrollment/retake", async (HttpContext context, string elderId, ElderFaceRetakeRequest request, ElderDbContext dbContext) =>
{
	var requestContext = context.GetPlatformRequestContext();
	if (requestContext is null || string.IsNullOrWhiteSpace(requestContext.TenantId))
	{
		return Results.Problem(title: "缺少租户上下文。", statusCode: StatusCodes.Status400BadRequest);
	}

	if (string.IsNullOrWhiteSpace(request.Reason))
	{
		return Results.Problem(title: "退回重录原因不能为空。", statusCode: StatusCodes.Status400BadRequest);
	}

	var elder = await dbContext.Elders.FirstOrDefaultAsync(item => item.ElderId == elderId && item.TenantId == requestContext.TenantId);
	if (elder is null)
	{
		return Results.Problem(title: $"老人 {elderId} 不存在。", statusCode: StatusCodes.Status404NotFound);
	}

	var now = DateTimeOffset.UtcNow;
	var reason = request.Reason.Trim();
	elder.FaceEnrollmentStatus = "需重录";
	elder.FaceCapturedSteps = [];
	elder.FaceQualityScore = 58;
	elder.FaceQualitySummary = SummarizeFaceQuality("需重录", [], elder.FaceQualityScore, reason);
	elder.FaceRetakeReason = reason;
	elder.FaceActivationNote = null;
	elder.FaceLastUpdatedUtc = now;

	dbContext.OutboxMessages.Add(new OutboxMessageEntity
	{
		OutboxMessageId = $"OUT-ELDER-FACE-RETAKE-{elderId}-{now.ToUnixTimeMilliseconds()}",
		TenantId = requestContext.TenantId,
		AggregateType = "ElderFaceEnrollment",
		AggregateId = elderId,
		EventType = "ElderFaceEnrollmentReturned",
		PayloadJson = System.Text.Json.JsonSerializer.Serialize(new
		{
			elderId,
			elder.FaceEnrollmentStatus,
			reason,
			now,
		}),
		CreatedAtUtc = now,
	});

	await dbContext.SaveChangesAsync();
	return Results.Ok(MapFaceEnrollment(elder));
}).RequireAuthorization();

app.Run();

static ElderProfileSummaryResponse MapProfileSummary(ElderProfileEntity elder)
{
	return new ElderProfileSummaryResponse(
		ElderId: elder.ElderId,
		TenantId: elder.TenantId,
		ElderName: elder.ElderName,
		Age: elder.Age,
		Gender: elder.Gender,
		CareLevel: elder.CareLevel,
		RoomNumber: elder.RoomNumber,
		AdmissionStatus: elder.AdmissionStatus,
		IdentityCard: elder.IdentityCard,
		BirthDate: elder.BirthDate,
		ElderPhone: elder.ElderPhone,
		FamilyContactName: elder.FamilyContactName,
		FamilyContactPhone: elder.FamilyContactPhone,
		AdlScore: elder.AdlScore,
		CognitiveLevel: elder.CognitiveLevel,
		MedicalAlerts: elder.MedicalAlerts,
		EntrustmentType: elder.EntrustmentType,
		EntrustmentOrganization: elder.EntrustmentOrganization,
		MonthlySubsidy: elder.MonthlySubsidy,
		ServiceItems: elder.ServiceItems,
		ServiceNotes: elder.ServiceNotes);
}

static AssessmentCaseResponse MapAssessmentCase(AdmissionRecordEntity admission, ElderProfileEntity elder)
{
	return new AssessmentCaseResponse(
		AssessmentId: admission.AdmissionId,
		ElderId: admission.ElderId,
		TenantId: admission.TenantId,
		ElderName: elder.ElderName,
		Age: elder.Age,
		Gender: elder.Gender,
		RoomNumber: admission.RoomNumber,
		Phone: admission.Phone,
		EmergencyContact: admission.EmergencyContact,
		RequestedCareLevel: admission.RequestedCareLevel,
		Status: admission.AssessmentStatus,
		ChronicConditions: admission.ChronicConditions,
		MedicationSummary: admission.MedicationSummary,
		AllergySummary: admission.AllergySummary,
		AdlScore: admission.AdlScore,
		CognitiveLevel: admission.CognitiveLevel,
		RiskNotes: admission.RiskNotes,
		EntrustmentType: elder.EntrustmentType,
		EntrustmentOrganization: elder.EntrustmentOrganization,
		MonthlySubsidy: elder.MonthlySubsidy,
		ServiceItems: elder.ServiceItems,
		ServiceNotes: elder.ServiceNotes,
		SourceType: string.IsNullOrWhiteSpace(admission.SourceType) ? "manual-form" : admission.SourceType,
		SourceLabel: admission.SourceLabel ?? GetAssessmentSourceLabel(admission.SourceType),
		SourceDocumentNames: admission.SourceDocumentNames,
		SourceSummary: admission.SourceSummary,
		AiRecommendation: new AssessmentAiRecommendationResponse(
			RecommendedLevel: admission.AiRecommendedCareLevel,
			Confidence: admission.AiConfidence,
			AssessmentScore: admission.AiAssessmentScore,
			ReasonSummary: admission.AiReasonSummary,
			Reasons: admission.AiReasons,
			FocusTags: admission.AiFocusTags,
			PlanTemplateCode: admission.AiPlanTemplateCode),
		ConfirmedCareLevel: admission.ConfirmedCareLevel,
		ReviewNote: admission.ReviewNote,
		ConfirmedAtUtc: admission.ConfirmedAtUtc,
		ConfirmedBy: admission.ConfirmedBy,
		CreatedAtUtc: admission.CreatedAtUtc);
}

static List<string> NormalizeStringList(IEnumerable<string>? values)
{
	if (values is null)
	{
		return [];
	}

	return values
		.Select(value => value.Trim())
		.Where(value => !string.IsNullOrWhiteSpace(value))
		.Distinct(StringComparer.Ordinal)
		.ToList();
}

static ElderFaceEnrollmentListItemResponse MapFaceEnrollment(ElderProfileEntity elder)
{
	var status = NormalizeOptionalText(elder.FaceEnrollmentStatus) ?? "待录入";
	var steps = NormalizeStringList(elder.FaceCapturedSteps);
	var qualityScore = elder.FaceQualityScore > 0 ? elder.FaceQualityScore : QualityForFaceSteps(steps);
	var summary = !string.IsNullOrWhiteSpace(elder.FaceQualitySummary)
		? elder.FaceQualitySummary
		: SummarizeFaceQuality(status, steps, qualityScore, elder.FaceRetakeReason);

	return new ElderFaceEnrollmentListItemResponse(
		ElderId: elder.ElderId,
		TenantId: elder.TenantId,
		ElderName: elder.ElderName,
		RoomNumber: elder.RoomNumber,
		CareLevel: elder.CareLevel,
		FaceEnrollmentStatus: status,
		FaceCapturedSteps: steps,
		FaceQualityScore: qualityScore,
		FaceQualitySummary: summary,
		FaceOperator: elder.FaceOperator,
		FaceDeviceLabel: elder.FaceDeviceLabel,
		FaceEntrySource: elder.FaceEntrySource,
		FaceLastUpdatedUtc: elder.FaceLastUpdatedUtc,
		FaceActivatedAtUtc: elder.FaceActivatedAtUtc,
		FaceActivationNote: elder.FaceActivationNote,
		FaceRetakeReason: elder.FaceRetakeReason);
}

static int QualityForFaceSteps(IReadOnlyCollection<string> steps)
{
	if (steps.Count >= 3)
	{
		return 92;
	}

	if (steps.Count == 2)
	{
		return 81;
	}

	if (steps.Count == 1)
	{
		return 68;
	}

	return 0;
}

static string SummarizeFaceQuality(string status, IReadOnlyCollection<string> steps, int qualityScore, string? retakeReason)
{
	if (status == "已生效")
	{
		return "三角度样本完整，当前模板已生效并可用于门禁或核验。";
	}

	if (status == "需重录")
	{
		return NormalizeOptionalText(retakeReason) ?? "当前质量不足，需重新采集。";
	}

	if (steps.Count == 0)
	{
		return "尚未开始采集，请先记录正脸、左侧脸和右侧脸样本。";
	}

	if (steps.Count < 3)
	{
		return $"已完成 {steps.Count}/3 个角度样本，继续补齐后再进入人工确认。";
	}

	return qualityScore >= 85 ? "样本已齐，可进入人工确认与激活。" : "样本已齐但质量偏低，建议退回重录。";
}

static string? NormalizeFaceCaptureStep(string? step)
{
	if (string.Equals(step, "front", StringComparison.OrdinalIgnoreCase))
	{
		return "front";
	}

	if (string.Equals(step, "left", StringComparison.OrdinalIgnoreCase))
	{
		return "left";
	}

	if (string.Equals(step, "right", StringComparison.OrdinalIgnoreCase))
	{
		return "right";
	}

	return null;
}

static string? NormalizeOptionalText(string? value)
{
	var trimmed = value?.Trim();
	return string.IsNullOrWhiteSpace(trimmed) ? null : trimmed;
}

static string NormalizeSourceType(string? value)
{
	if (string.Equals(value, "document-import", StringComparison.OrdinalIgnoreCase))
	{
		return "document-import";
	}

	return "manual-form";
}

static string GetAssessmentSourceLabel(string? sourceType)
{
	return string.Equals(sourceType, "document-import", StringComparison.OrdinalIgnoreCase)
		? "资料导入"
		: "前台建档";
}

static List<string> BuildMedicalAlerts(AssessmentCaseCreateRequest request)
{
	return NormalizeStringList([
		request.ChronicConditions,
		request.AllergySummary,
		request.RiskNotes,
	]);
}

static decimal? NormalizeMonthlySubsidy(decimal? value)
{
	return value is > 0 ? Math.Round(value.Value, 2) : null;
}

static int? NormalizeAdlScore(int? value)
{
	return value is >= 0 and <= 100 ? value : null;
}
