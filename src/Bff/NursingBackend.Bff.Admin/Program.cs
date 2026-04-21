using NursingBackend.Bff.Admin;
using NursingBackend.BuildingBlocks.Context;
using NursingBackend.BuildingBlocks.Contracts;
using NursingBackend.BuildingBlocks.Hosting;
using NursingBackend.BuildingBlocks.Networking;

var builder = WebApplication.CreateBuilder(args);
builder.AddPlatformDefaults();

var app = builder.Build();
app.MapPlatformEndpoints(new PlatformServiceDescriptor(
	ServiceName: "admin-bff",
	ServiceType: "bff",
	BoundedContext: "admin-edge",
	Consumers: ["admin-web"],
	Capabilities: ["dashboard-aggregation", "approval-views", "analytics-dto", "ai-operations-view"]));

app.MapPost("/api/admin/admissions/onboard", async (AdminAdmissionOnboardRequest request, HttpContext context, IHttpClientFactory httpClientFactory, IConfiguration configuration, CancellationToken cancellationToken) =>
{
	var requestContext = context.GetPlatformRequestContext();
	if (requestContext is null || string.IsNullOrWhiteSpace(requestContext.TenantId))
	{
		return Results.Problem(title: "缺少租户上下文。", statusCode: StatusCodes.Status400BadRequest);
	}

	try
	{
		var client = httpClientFactory.CreateClient();

		var identity = await GetJsonAsync<IdentityContextResponse>(
			client,
			context,
			$"{ResolveServiceUrl(configuration, "Identity", "http://localhost:5265")}/api/identity/me",
			cancellationToken);

		var tenant = await GetJsonAsync<TenantDescriptorResponse>(
			client,
			context,
			$"{ResolveServiceUrl(configuration, "Tenant", "http://localhost:5186")}/api/tenants/{requestContext.TenantId}",
			cancellationToken);

		var admission = await PostJsonAsync<AdmissionRecordResponse>(
			client,
			context,
			$"{ResolveServiceUrl(configuration, "Elder", "http://localhost:5062")}/api/elders/admissions",
			new AdmissionCreateRequest(
				AdmissionReference: request.AdmissionReference,
				ElderName: request.ElderName,
				Age: request.Age,
				Gender: request.Gender,
				CareLevel: request.CareLevel,
				RoomNumber: request.RoomNumber,
				FamilyContactName: request.FamilyContactName,
				FamilyContactPhone: request.FamilyContactPhone,
				MedicalAlerts: request.MedicalAlerts,
				EntrustmentType: request.EntrustmentType,
				EntrustmentOrganization: request.EntrustmentOrganization,
				MonthlySubsidy: request.MonthlySubsidy,
				ServiceItems: request.ServiceItems,
				ServiceNotes: request.ServiceNotes),
			cancellationToken);

		var healthArchive = await PostJsonAsync<HealthArchiveSummaryResponse>(
			client,
			context,
			$"{ResolveServiceUrl(configuration, "Health", "http://localhost:5197")}/api/health/archives/from-admission",
			new HealthArchiveCreateFromAdmissionRequest(
				AdmissionId: admission.AdmissionId,
				ElderId: admission.ElderId,
				ElderName: admission.ElderName,
				CareLevel: admission.CareLevel,
				BloodPressure: request.BloodPressure,
				HeartRate: request.HeartRate,
				Temperature: request.Temperature,
				BloodSugar: request.BloodSugar,
				Oxygen: request.Oxygen,
				AlertSummary: request.AlertSummary),
			cancellationToken);

		var carePlan = await PostJsonAsync<CarePlanResponse>(
			client,
			context,
			$"{ResolveServiceUrl(configuration, "Care", "http://localhost:5019")}/api/care/plans/from-admission",
			new CarePlanCreateFromAdmissionRequest(
				AdmissionId: admission.AdmissionId,
				ElderId: admission.ElderId,
				ElderName: admission.ElderName,
				CareLevel: admission.CareLevel,
				RoomNumber: admission.RoomNumber),
			cancellationToken);

		var familyNotification = await PostJsonAsync<NotificationMessageResponse>(
			client,
			context,
			$"{ResolveServiceUrl(configuration, "Notification", "http://localhost:5144")}/api/notifications/dispatch",
			new NotificationDispatchRequest(
				Audience: "family",
				AudienceKey: admission.ElderId,
				Category: "admission",
				Title: $"{admission.ElderName} 已完成入住建档",
				Body: $"已生成健康档案与护理计划，房间 {admission.RoomNumber}，护理等级 {admission.CareLevel}。",
				SourceService: "admin-bff",
				SourceEntityId: admission.AdmissionId,
				CorrelationId: requestContext.CorrelationId),
			cancellationToken);

		var naniNotifications = await GetJsonAsync<IReadOnlyList<NotificationMessageResponse>>(
			client,
			context,
			$"{ResolveServiceUrl(configuration, "Notification", "http://localhost:5144")}/api/notifications?audience=nani&audienceKey={admission.ElderId}",
			cancellationToken) ?? [];

		var notifications = new List<NotificationMessageResponse> { familyNotification };
		notifications.AddRange(naniNotifications);

		return Results.Ok(new AdminAdmissionOnboardResponse(
			Admission: admission,
			HealthArchive: healthArchive,
			CarePlan: carePlan,
			Notifications: notifications,
			Tenant: tenant,
			Operator: identity,
			CorrelationId: requestContext.CorrelationId));
	}
	catch (Exception exception)
	{
		return Results.Problem(title: "入住纵切编排失败。", detail: exception.Message, statusCode: StatusCodes.Status502BadGateway);
	}
}).RequireAuthorization();

app.MapGet("/api/admin/nursing/workflow-board", async (HttpContext context, IHttpClientFactory httpClientFactory, IConfiguration configuration, CancellationToken cancellationToken) =>
{
	try
	{
		var client = httpClientFactory.CreateClient();
		var response = await GetJsonAsync<NursingWorkflowBoardResponse>(
			client,
			context,
			$"{ResolveServiceUrl(configuration, "Care", "http://localhost:5019")}/api/care/admin/workflow-board",
			cancellationToken);
		return Results.Ok(response);
	}
	catch (Exception exception)
	{
		return Results.Problem(title: "护理工作流看板查询失败。", detail: exception.Message, statusCode: StatusCodes.Status502BadGateway);
	}
}).RequireAuthorization();

app.MapGet("/api/admin/nursing/observability", async (HttpContext context, IHttpClientFactory httpClientFactory, IConfiguration configuration, CancellationToken cancellationToken) =>
{
	try
	{
		var client = httpClientFactory.CreateClient();
		var response = await GetJsonAsync<CareWorkflowObservabilityResponse>(
			client,
			context,
			$"{ResolveServiceUrl(configuration, "Care", "http://localhost:5019")}/api/care/admin/observability",
			cancellationToken);
		return Results.Ok(response);
	}
	catch (Exception exception)
	{
		return Results.Problem(title: "护理工作流观测查询失败。", detail: exception.Message, statusCode: StatusCodes.Status502BadGateway);
	}
}).RequireAuthorization();

app.MapGet("/api/admin/nursing/audits", async (HttpContext context, IHttpClientFactory httpClientFactory, IConfiguration configuration, int? take, CancellationToken cancellationToken) =>
{
	try
	{
		var client = httpClientFactory.CreateClient();
		var suffix = take is > 0 ? $"?take={take.Value}" : string.Empty;
		var response = await GetJsonAsync<IReadOnlyList<CareWorkflowAuditResponse>>(
			client,
			context,
			$"{ResolveServiceUrl(configuration, "Care", "http://localhost:5019")}/api/care/admin/audits{suffix}",
			cancellationToken);
		return Results.Ok(response);
	}
	catch (Exception exception)
	{
		return Results.Problem(title: "护理工作流审计查询失败。", detail: exception.Message, statusCode: StatusCodes.Status502BadGateway);
	}
}).RequireAuthorization();

app.MapPost("/api/admin/nursing/packages", async (CreateServicePackageRequest request, HttpContext context, IHttpClientFactory httpClientFactory, IConfiguration configuration, CancellationToken cancellationToken) =>
{
	try
	{
		var client = httpClientFactory.CreateClient();
		var response = await PostJsonAsync<ServicePackageResponse>(
			client,
			context,
			$"{ResolveServiceUrl(configuration, "Care", "http://localhost:5019")}/api/care/admin/packages",
			request,
			cancellationToken);
		return Results.Ok(response);
	}
	catch (Exception exception)
	{
		return Results.Problem(title: "创建护理套餐失败。", detail: exception.Message, statusCode: StatusCodes.Status502BadGateway);
	}
}).RequireAuthorization();

app.MapPost("/api/admin/nursing/packages/{packageId}/actions/{action}", async (string packageId, string action, HttpContext context, IHttpClientFactory httpClientFactory, IConfiguration configuration, CancellationToken cancellationToken) =>
{
	try
	{
		var client = httpClientFactory.CreateClient();
		var response = await PostJsonAsync<ServicePackageActionResponse>(
			client,
			context,
			$"{ResolveServiceUrl(configuration, "Care", "http://localhost:5019")}/api/care/admin/packages/{packageId}/actions/{action}",
			new { },
			cancellationToken);
		return Results.Ok(response);
	}
	catch (Exception exception)
	{
		return Results.Problem(title: "护理套餐动作提交失败。", detail: exception.Message, statusCode: StatusCodes.Status502BadGateway);
	}
}).RequireAuthorization();

app.MapPost("/api/admin/nursing/packages/{packageId}/plans", async (string packageId, HttpContext context, IHttpClientFactory httpClientFactory, IConfiguration configuration, CancellationToken cancellationToken) =>
{
	try
	{
		var client = httpClientFactory.CreateClient();
		var response = await PostJsonAsync<ServicePlanResponse>(
			client,
			context,
			$"{ResolveServiceUrl(configuration, "Care", "http://localhost:5019")}/api/care/admin/packages/{packageId}/plans",
			new { },
			cancellationToken);
		return Results.Ok(response);
	}
	catch (Exception exception)
	{
		return Results.Problem(title: "从护理套餐生成计划失败。", detail: exception.Message, statusCode: StatusCodes.Status502BadGateway);
	}
}).RequireAuthorization();

app.MapPost("/api/admin/nursing/plans", async (CreateServicePlanRequest request, HttpContext context, IHttpClientFactory httpClientFactory, IConfiguration configuration, CancellationToken cancellationToken) =>
{
	try
	{
		var client = httpClientFactory.CreateClient();
		var response = await PostJsonAsync<ServicePlanResponse>(
			client,
			context,
			$"{ResolveServiceUrl(configuration, "Care", "http://localhost:5019")}/api/care/admin/plans",
			request,
			cancellationToken);
		return Results.Ok(response);
	}
	catch (Exception exception)
	{
		return Results.Problem(title: "创建护理计划失败。", detail: exception.Message, statusCode: StatusCodes.Status502BadGateway);
	}
}).RequireAuthorization();

app.MapPost("/api/admin/nursing/plans/{planId}/actions/{action}", async (string planId, string action, HttpContext context, IHttpClientFactory httpClientFactory, IConfiguration configuration, CancellationToken cancellationToken) =>
{
	try
	{
		var client = httpClientFactory.CreateClient();
		var response = await PostJsonAsync<ServicePlanActionResponse>(
			client,
			context,
			$"{ResolveServiceUrl(configuration, "Care", "http://localhost:5019")}/api/care/admin/plans/{planId}/actions/{action}",
			new { },
			cancellationToken);
		return Results.Ok(response);
	}
	catch (Exception exception)
	{
		return Results.Problem(title: "护理计划动作提交失败。", detail: exception.Message, statusCode: StatusCodes.Status502BadGateway);
	}
}).RequireAuthorization();

app.MapPost("/api/admin/nursing/tasks/{taskId}/start", async (string taskId, ServicePlanTaskActionRequest request, HttpContext context, IHttpClientFactory httpClientFactory, IConfiguration configuration, CancellationToken cancellationToken) =>
{
	try
	{
		var client = httpClientFactory.CreateClient();
		var response = await PostJsonAsync<ServicePlanActionResponse>(
			client,
			context,
			$"{ResolveServiceUrl(configuration, "Care", "http://localhost:5019")}/api/care/admin/tasks/{taskId}/start",
			request,
			cancellationToken);
		return Results.Ok(response);
	}
	catch (Exception exception)
	{
		return Results.Problem(title: "护理任务开始执行失败。", detail: exception.Message, statusCode: StatusCodes.Status502BadGateway);
	}
}).RequireAuthorization();

app.MapPost("/api/admin/nursing/tasks/{taskId}/complete", async (string taskId, ServicePlanTaskActionRequest request, HttpContext context, IHttpClientFactory httpClientFactory, IConfiguration configuration, CancellationToken cancellationToken) =>
{
	try
	{
		var client = httpClientFactory.CreateClient();
		var response = await PostJsonAsync<ServicePlanActionResponse>(
			client,
			context,
			$"{ResolveServiceUrl(configuration, "Care", "http://localhost:5019")}/api/care/admin/tasks/{taskId}/complete",
			request,
			cancellationToken);
		return Results.Ok(response);
	}
	catch (Exception exception)
	{
		return Results.Problem(title: "护理任务完成失败。", detail: exception.Message, statusCode: StatusCodes.Status502BadGateway);
	}
}).RequireAuthorization();

app.MapPut("/api/admin/nursing/tasks/{taskId}/note", async (string taskId, SaveServicePlanTaskNoteRequest request, HttpContext context, IHttpClientFactory httpClientFactory, IConfiguration configuration, CancellationToken cancellationToken) =>
{
	try
	{
		var client = httpClientFactory.CreateClient();
		var response = await PutJsonAsync<ServicePlanActionResponse>(
			client,
			context,
			$"{ResolveServiceUrl(configuration, "Care", "http://localhost:5019")}/api/care/admin/tasks/{taskId}/note",
			request,
			cancellationToken);
		return Results.Ok(response);
	}
	catch (Exception exception)
	{
		return Results.Problem(title: "护理任务备注保存失败。", detail: exception.Message, statusCode: StatusCodes.Status502BadGateway);
	}
}).RequireAuthorization();

// ── Content Management Proxy (Config Service) ──────────────────────────────

app.MapGet("/api/admin/static-texts", async (HttpContext context, IHttpClientFactory httpClientFactory, IConfiguration configuration, CancellationToken cancellationToken, string? ns, string? locale, string? keyword, int page = 1, int pageSize = 20) =>
{
	try
	{
		var client = httpClientFactory.CreateClient();
		var qs = $"?page={page}&pageSize={pageSize}";
		if (!string.IsNullOrWhiteSpace(ns)) qs += $"&ns={Uri.EscapeDataString(ns)}";
		if (!string.IsNullOrWhiteSpace(locale)) qs += $"&locale={Uri.EscapeDataString(locale)}";
		if (!string.IsNullOrWhiteSpace(keyword)) qs += $"&keyword={Uri.EscapeDataString(keyword)}";
		var response = await GetJsonAsync<StaticTextListResponse>(client, context, $"{ResolveServiceUrl(configuration, "Config", "http://localhost:5290")}/api/config/static-texts{qs}", cancellationToken);
		return Results.Ok(response);
	}
	catch (Exception ex) { return Results.Problem(title: "静态文本查询失败。", detail: ex.Message, statusCode: StatusCodes.Status502BadGateway); }
}).RequireAuthorization();

app.MapGet("/api/admin/static-texts/{id}", async (string id, HttpContext context, IHttpClientFactory httpClientFactory, IConfiguration configuration, CancellationToken cancellationToken) =>
{
	try
	{
		var client = httpClientFactory.CreateClient();
		var response = await GetJsonAsync<StaticTextResponse>(client, context, $"{ResolveServiceUrl(configuration, "Config", "http://localhost:5290")}/api/config/static-texts/{id}", cancellationToken);
		return Results.Ok(response);
	}
	catch (Exception ex) { return Results.Problem(title: "静态文本查询失败。", detail: ex.Message, statusCode: StatusCodes.Status502BadGateway); }
}).RequireAuthorization();

app.MapPost("/api/admin/static-texts", async (StaticTextCreateRequest request, HttpContext context, IHttpClientFactory httpClientFactory, IConfiguration configuration, CancellationToken cancellationToken) =>
{
	try
	{
		var client = httpClientFactory.CreateClient();
		var response = await PostJsonAsync<StaticTextResponse>(client, context, $"{ResolveServiceUrl(configuration, "Config", "http://localhost:5290")}/api/config/static-texts", request, cancellationToken);
		return Results.Ok(response);
	}
	catch (Exception ex) { return Results.Problem(title: "静态文本创建失败。", detail: ex.Message, statusCode: StatusCodes.Status502BadGateway); }
}).RequireAuthorization();

app.MapPut("/api/admin/static-texts/{id}", async (string id, StaticTextUpdateRequest request, HttpContext context, IHttpClientFactory httpClientFactory, IConfiguration configuration, CancellationToken cancellationToken) =>
{
	try
	{
		var client = httpClientFactory.CreateClient();
		var response = await PutJsonAsync<StaticTextResponse>(client, context, $"{ResolveServiceUrl(configuration, "Config", "http://localhost:5290")}/api/config/static-texts/{id}", request, cancellationToken);
		return Results.Ok(response);
	}
	catch (Exception ex) { return Results.Problem(title: "静态文本更新失败。", detail: ex.Message, statusCode: StatusCodes.Status502BadGateway); }
}).RequireAuthorization();

app.MapDelete("/api/admin/static-texts/{id}", async (string id, HttpContext context, IHttpClientFactory httpClientFactory, IConfiguration configuration, CancellationToken cancellationToken) =>
{
	try
	{
		var client = httpClientFactory.CreateClient();
		using var req = DownstreamHttp.CreateJsonRequest(HttpMethod.Delete, $"{ResolveServiceUrl(configuration, "Config", "http://localhost:5290")}/api/config/static-texts/{id}", context);
		using var resp = await client.SendAsync(req, cancellationToken);
		resp.EnsureSuccessStatusCode();
		return Results.Ok(new { success = true });
	}
	catch (Exception ex) { return Results.Problem(title: "静态文本删除失败。", detail: ex.Message, statusCode: StatusCodes.Status502BadGateway); }
}).RequireAuthorization();

app.MapGet("/api/admin/static-texts/namespaces", async (HttpContext context, IHttpClientFactory httpClientFactory, IConfiguration configuration, CancellationToken cancellationToken) =>
{
	try
	{
		var client = httpClientFactory.CreateClient();
		var response = await GetJsonAsync<List<string>>(client, context, $"{ResolveServiceUrl(configuration, "Config", "http://localhost:5290")}/api/config/static-texts/namespaces", cancellationToken);
		return Results.Ok(response);
	}
	catch (Exception ex) { return Results.Problem(title: "命名空间查询失败。", detail: ex.Message, statusCode: StatusCodes.Status502BadGateway); }
}).RequireAuthorization();

app.MapGet("/api/admin/option-groups", async (HttpContext context, IHttpClientFactory httpClientFactory, IConfiguration configuration, CancellationToken cancellationToken, string? status, string? keyword) =>
{
	try
	{
		var client = httpClientFactory.CreateClient();
		var qs = "";
		if (!string.IsNullOrWhiteSpace(status)) qs += $"?status={Uri.EscapeDataString(status)}";
		if (!string.IsNullOrWhiteSpace(keyword)) qs += $"{(qs.Length > 0 ? "&" : "?")}keyword={Uri.EscapeDataString(keyword)}";
		var response = await GetJsonAsync<OptionGroupListResponse>(client, context, $"{ResolveServiceUrl(configuration, "Config", "http://localhost:5290")}/api/config/option-groups{qs}", cancellationToken);
		return Results.Ok(response);
	}
	catch (Exception ex) { return Results.Problem(title: "选项分组查询失败。", detail: ex.Message, statusCode: StatusCodes.Status502BadGateway); }
}).RequireAuthorization();

app.MapPost("/api/admin/option-groups", async (OptionGroupCreateRequest request, HttpContext context, IHttpClientFactory httpClientFactory, IConfiguration configuration, CancellationToken cancellationToken) =>
{
	try
	{
		var client = httpClientFactory.CreateClient();
		var response = await PostJsonAsync<OptionGroupResponse>(client, context, $"{ResolveServiceUrl(configuration, "Config", "http://localhost:5290")}/api/config/option-groups", request, cancellationToken);
		return Results.Ok(response);
	}
	catch (Exception ex) { return Results.Problem(title: "选项分组创建失败。", detail: ex.Message, statusCode: StatusCodes.Status502BadGateway); }
}).RequireAuthorization();

app.MapPut("/api/admin/option-groups/{id}", async (string id, OptionGroupUpdateRequest request, HttpContext context, IHttpClientFactory httpClientFactory, IConfiguration configuration, CancellationToken cancellationToken) =>
{
	try
	{
		var client = httpClientFactory.CreateClient();
		var response = await PutJsonAsync<OptionGroupResponse>(client, context, $"{ResolveServiceUrl(configuration, "Config", "http://localhost:5290")}/api/config/option-groups/{id}", request, cancellationToken);
		return Results.Ok(response);
	}
	catch (Exception ex) { return Results.Problem(title: "选项分组更新失败。", detail: ex.Message, statusCode: StatusCodes.Status502BadGateway); }
}).RequireAuthorization();

app.MapDelete("/api/admin/option-groups/{id}", async (string id, HttpContext context, IHttpClientFactory httpClientFactory, IConfiguration configuration, CancellationToken cancellationToken) =>
{
	try
	{
		var client = httpClientFactory.CreateClient();
		using var req = DownstreamHttp.CreateJsonRequest(HttpMethod.Delete, $"{ResolveServiceUrl(configuration, "Config", "http://localhost:5290")}/api/config/option-groups/{id}", context);
		using var resp = await client.SendAsync(req, cancellationToken);
		resp.EnsureSuccessStatusCode();
		return Results.Ok(new { success = true });
	}
	catch (Exception ex) { return Results.Problem(title: "选项分组删除失败。", detail: ex.Message, statusCode: StatusCodes.Status502BadGateway); }
}).RequireAuthorization();

app.MapGet("/api/admin/option-groups/{groupId}/items", async (string groupId, HttpContext context, IHttpClientFactory httpClientFactory, IConfiguration configuration, CancellationToken cancellationToken) =>
{
	try
	{
		var client = httpClientFactory.CreateClient();
		var response = await GetJsonAsync<List<OptionItemResponse>>(client, context, $"{ResolveServiceUrl(configuration, "Config", "http://localhost:5290")}/api/config/option-groups/{groupId}/items", cancellationToken);
		return Results.Ok(response);
	}
	catch (Exception ex) { return Results.Problem(title: "选项列表查询失败。", detail: ex.Message, statusCode: StatusCodes.Status502BadGateway); }
}).RequireAuthorization();

app.MapPost("/api/admin/option-groups/{groupId}/items", async (string groupId, OptionItemCreateRequest request, HttpContext context, IHttpClientFactory httpClientFactory, IConfiguration configuration, CancellationToken cancellationToken) =>
{
	try
	{
		var client = httpClientFactory.CreateClient();
		var response = await PostJsonAsync<OptionItemResponse>(client, context, $"{ResolveServiceUrl(configuration, "Config", "http://localhost:5290")}/api/config/option-groups/{groupId}/items", request, cancellationToken);
		return Results.Ok(response);
	}
	catch (Exception ex) { return Results.Problem(title: "选项创建失败。", detail: ex.Message, statusCode: StatusCodes.Status502BadGateway); }
}).RequireAuthorization();

app.MapGet("/api/admin/audit-logs", async (HttpContext context, IHttpClientFactory httpClientFactory, IConfiguration configuration, CancellationToken cancellationToken, string? resourceType, string? operatorId, int page = 1, int pageSize = 20) =>
{
	try
	{
		var client = httpClientFactory.CreateClient();
		var qs = $"?page={page}&pageSize={pageSize}";
		if (!string.IsNullOrWhiteSpace(resourceType)) qs += $"&resourceType={Uri.EscapeDataString(resourceType)}";
		if (!string.IsNullOrWhiteSpace(operatorId)) qs += $"&operatorId={Uri.EscapeDataString(operatorId)}";
		var response = await GetJsonAsync<ContentAuditLogListResponse>(client, context, $"{ResolveServiceUrl(configuration, "Config", "http://localhost:5290")}/api/config/audit-logs{qs}", cancellationToken);
		return Results.Ok(response);
	}
	catch (Exception ex) { return Results.Problem(title: "操作日志查询失败。", detail: ex.Message, statusCode: StatusCodes.Status502BadGateway); }
}).RequireAuthorization();

// ── Option Item CRUD Proxy (Config Service) ────────────────────────────────

app.MapPut("/api/admin/option-groups/{groupId}/items/{itemId}", async (string groupId, string itemId, OptionItemUpdateRequest request, HttpContext context, IHttpClientFactory httpClientFactory, IConfiguration configuration, CancellationToken cancellationToken) =>
{
	try
	{
		var client = httpClientFactory.CreateClient();
		var response = await PutJsonAsync<OptionItemResponse>(client, context, $"{ResolveServiceUrl(configuration, "Config", "http://localhost:5290")}/api/config/option-groups/{groupId}/items/{itemId}", request, cancellationToken);
		return Results.Ok(response);
	}
	catch (Exception ex) { return Results.Problem(title: "选项更新失败。", detail: ex.Message, statusCode: StatusCodes.Status502BadGateway); }
}).RequireAuthorization();

app.MapDelete("/api/admin/option-groups/{groupId}/items/{itemId}", async (string groupId, string itemId, HttpContext context, IHttpClientFactory httpClientFactory, IConfiguration configuration, CancellationToken cancellationToken) =>
{
	try
	{
		var client = httpClientFactory.CreateClient();
		using var req = DownstreamHttp.CreateJsonRequest(HttpMethod.Delete, $"{ResolveServiceUrl(configuration, "Config", "http://localhost:5290")}/api/config/option-groups/{groupId}/items/{itemId}", context);
		using var resp = await client.SendAsync(req, cancellationToken);
		resp.EnsureSuccessStatusCode();
		return Results.Ok(new { success = true });
	}
	catch (Exception ex) { return Results.Problem(title: "选项删除失败。", detail: ex.Message, statusCode: StatusCodes.Status502BadGateway); }
}).RequireAuthorization();

app.MapPatch("/api/admin/option-groups/{groupId}/items/{itemId}/toggle", async (string groupId, string itemId, HttpContext context, IHttpClientFactory httpClientFactory, IConfiguration configuration, CancellationToken cancellationToken) =>
{
	try
	{
		var client = httpClientFactory.CreateClient();
		using var req = DownstreamHttp.CreateJsonRequest(HttpMethod.Patch, $"{ResolveServiceUrl(configuration, "Config", "http://localhost:5290")}/api/config/option-groups/{groupId}/items/{itemId}/toggle", context);
		using var resp = await client.SendAsync(req, cancellationToken);
		resp.EnsureSuccessStatusCode();
		return Results.Ok(await resp.ReadJsonAsync<OptionItemResponse>(cancellationToken));
	}
	catch (Exception ex) { return Results.Problem(title: "选项状态切换失败。", detail: ex.Message, statusCode: StatusCodes.Status502BadGateway); }
}).RequireAuthorization();

app.MapPut("/api/admin/option-groups/{groupId}/items/reorder", async (string groupId, OptionItemReorderRequest request, HttpContext context, IHttpClientFactory httpClientFactory, IConfiguration configuration, CancellationToken cancellationToken) =>
{
	try
	{
		var client = httpClientFactory.CreateClient();
		var response = await PutJsonAsync<List<OptionItemResponse>>(client, context, $"{ResolveServiceUrl(configuration, "Config", "http://localhost:5290")}/api/config/option-groups/{groupId}/items/reorder", request, cancellationToken);
		return Results.Ok(response);
	}
	catch (Exception ex) { return Results.Problem(title: "选项排序失败。", detail: ex.Message, statusCode: StatusCodes.Status502BadGateway); }
}).RequireAuthorization();

app.MapGet("/api/admin/audit-logs/{resourceType}/{resourceId}", async (string resourceType, string resourceId, HttpContext context, IHttpClientFactory httpClientFactory, IConfiguration configuration, CancellationToken cancellationToken) =>
{
	try
	{
		var client = httpClientFactory.CreateClient();
		var response = await GetJsonAsync<ContentAuditLogListResponse>(client, context, $"{ResolveServiceUrl(configuration, "Config", "http://localhost:5290")}/api/config/audit-logs/{Uri.EscapeDataString(resourceType)}/{Uri.EscapeDataString(resourceId)}", cancellationToken);
		return Results.Ok(response);
	}
	catch (Exception ex) { return Results.Problem(title: "资源审计日志查询失败。", detail: ex.Message, statusCode: StatusCodes.Status502BadGateway); }
}).RequireAuthorization();

// ── Elder Service Proxy ────────────────────────────────────────────────────

app.MapGet("/api/admin/elders", async (HttpContext context, IHttpClientFactory httpClientFactory, IConfiguration configuration, CancellationToken cancellationToken, string? name, string? status, string? careLevel, int page = 1, int pageSize = 20) =>
{
	try
	{
		var client = httpClientFactory.CreateClient();
		var qs = $"?page={page}&pageSize={pageSize}";
		if (!string.IsNullOrWhiteSpace(name)) qs += $"&name={Uri.EscapeDataString(name)}";
		if (!string.IsNullOrWhiteSpace(status)) qs += $"&status={Uri.EscapeDataString(status)}";
		if (!string.IsNullOrWhiteSpace(careLevel)) qs += $"&careLevel={Uri.EscapeDataString(careLevel)}";
		var response = await GetJsonAsync<ElderListResponse>(client, context, $"{ResolveServiceUrl(configuration, "Elder", "http://localhost:5062")}/api/elders{qs}", cancellationToken);
		return Results.Ok(response);
	}
	catch (Exception ex) { return Results.Problem(title: "老人列表查询失败。", detail: ex.Message, statusCode: StatusCodes.Status502BadGateway); }
}).RequireAuthorization();

app.MapPost("/api/admin/elders/admissions", async (AdmissionCreateRequest request, HttpContext context, IHttpClientFactory httpClientFactory, IConfiguration configuration, CancellationToken cancellationToken) =>
{
	try
	{
		var client = httpClientFactory.CreateClient();
		var response = await PostJsonAsync<AdmissionRecordResponse>(client, context, $"{ResolveServiceUrl(configuration, "Elder", "http://localhost:5062")}/api/elders/admissions", request, cancellationToken);
		return Results.Ok(response);
	}
	catch (Exception ex) { return Results.Problem(title: "长者入住建档失败。", detail: ex.Message, statusCode: StatusCodes.Status502BadGateway); }
}).RequireAuthorization();

app.MapGet("/api/admin/assessments", async (HttpContext context, IHttpClientFactory httpClientFactory, IConfiguration configuration, CancellationToken cancellationToken, string? keyword, string? status, string? sourceType, string? scene, int page = 1, int pageSize = 20) =>
{
	try
	{
		var client = httpClientFactory.CreateClient();
		var qs = $"?page={page}&pageSize={pageSize}";
		if (!string.IsNullOrWhiteSpace(keyword)) qs += $"&keyword={Uri.EscapeDataString(keyword)}";
		if (!string.IsNullOrWhiteSpace(status)) qs += $"&status={Uri.EscapeDataString(status)}";
		if (!string.IsNullOrWhiteSpace(sourceType)) qs += $"&sourceType={Uri.EscapeDataString(sourceType)}";
		if (!string.IsNullOrWhiteSpace(scene)) qs += $"&scene={Uri.EscapeDataString(scene)}";
		var response = await GetJsonAsync<AssessmentCaseListResponse>(client, context, $"{ResolveServiceUrl(configuration, "Elder", "http://localhost:5062")}/api/elders/assessments{qs}", cancellationToken);
		return Results.Ok(response);
	}
	catch (Exception ex) { return Results.Problem(title: "个案评定列表查询失败。", detail: ex.Message, statusCode: StatusCodes.Status502BadGateway); }
}).RequireAuthorization();

app.MapPost("/api/admin/assessments", async (AdminAssessmentCaseCreateRequest request, HttpContext context, IHttpClientFactory httpClientFactory, IConfiguration configuration, CancellationToken cancellationToken) =>
{
	try
	{
		var client = httpClientFactory.CreateClient();
		var aiResponse = await PostJsonAsync<AiAdmissionAssessmentResponse>(
			client,
			context,
			$"{ResolveServiceUrl(configuration, "AiOrchestration", "http://localhost:5267")}/api/ai/admission-assessment",
			new AiAdmissionAssessmentRequest(
				ElderName: request.ElderName,
				Age: request.Age,
				Gender: request.Gender,
				RequestedCareLevel: request.RequestedCareLevel,
				MedicalAlerts: BuildAssessmentMedicalAlerts(request),
				FamilyNotes: request.RiskNotes),
			cancellationToken);

		var response = await PostJsonAsync<AssessmentCaseResponse>(
			client,
			context,
			$"{ResolveServiceUrl(configuration, "Elder", "http://localhost:5062")}/api/elders/assessments",
			new AssessmentCaseCreateRequest(
				ElderName: request.ElderName,
				Age: request.Age,
				Gender: request.Gender,
				Phone: request.Phone,
				EmergencyContact: request.EmergencyContact,
				RoomNumber: request.RoomNumber,
				RequestedCareLevel: request.RequestedCareLevel,
				ChronicConditions: request.ChronicConditions,
				MedicationSummary: request.MedicationSummary,
				AllergySummary: request.AllergySummary,
				AdlScore: request.AdlScore,
				CognitiveLevel: request.CognitiveLevel,
				RiskNotes: request.RiskNotes,
				EntrustmentType: request.EntrustmentType,
				EntrustmentOrganization: request.EntrustmentOrganization,
				MonthlySubsidy: request.MonthlySubsidy,
				ServiceItems: request.ServiceItems,
				ServiceNotes: request.ServiceNotes,
				SourceType: request.SourceType,
				SourceLabel: request.SourceLabel,
				SourceDocumentNames: request.SourceDocumentNames,
				SourceSummary: request.SourceSummary,
				AiRecommendation: BuildAssessmentAiRecommendation(request, aiResponse)),
			cancellationToken);

		return Results.Ok(response);
	}
	catch (Exception ex) { return Results.Problem(title: "个案评定创建失败。", detail: ex.Message, statusCode: StatusCodes.Status502BadGateway); }
}).RequireAuthorization();

app.MapPut("/api/admin/assessments/{assessmentId}/decision", async (string assessmentId, AssessmentDecisionUpdateRequest request, HttpContext context, IHttpClientFactory httpClientFactory, IConfiguration configuration, CancellationToken cancellationToken) =>
{
	try
	{
		var client = httpClientFactory.CreateClient();
		var response = await PutJsonAsync<AssessmentCaseResponse>(client, context, $"{ResolveServiceUrl(configuration, "Elder", "http://localhost:5062")}/api/elders/assessments/{Uri.EscapeDataString(assessmentId)}/decision", request, cancellationToken);
		return Results.Ok(response);
	}
	catch (Exception ex) { return Results.Problem(title: "个案认定确认失败。", detail: ex.Message, statusCode: StatusCodes.Status502BadGateway); }
}).RequireAuthorization();

app.MapPut("/api/admin/assessments/{assessmentId}/activate", async (string assessmentId, HttpContext context, IHttpClientFactory httpClientFactory, IConfiguration configuration, CancellationToken cancellationToken) =>
{
	try
	{
		var client = httpClientFactory.CreateClient();
		var response = await PutJsonAsync<AssessmentCaseResponse>(client, context, $"{ResolveServiceUrl(configuration, "Elder", "http://localhost:5062")}/api/elders/assessments/{Uri.EscapeDataString(assessmentId)}/activate", new { }, cancellationToken);
		return Results.Ok(response);
	}
	catch (Exception ex) { return Results.Problem(title: "个案认定生效失败。", detail: ex.Message, statusCode: StatusCodes.Status502BadGateway); }
}).RequireAuthorization();

app.MapGet("/api/admin/elders/{elderId}", async (string elderId, HttpContext context, IHttpClientFactory httpClientFactory, IConfiguration configuration, CancellationToken cancellationToken) =>
{
	try
	{
		var client = httpClientFactory.CreateClient();
		var response = await GetJsonAsync<ElderProfileSummaryResponse>(client, context, $"{ResolveServiceUrl(configuration, "Elder", "http://localhost:5062")}/api/elders/{Uri.EscapeDataString(elderId)}", cancellationToken);
		return Results.Ok(response);
	}
	catch (Exception ex) { return Results.Problem(title: "老人详情查询失败。", detail: ex.Message, statusCode: StatusCodes.Status502BadGateway); }
}).RequireAuthorization();

app.MapPut("/api/admin/elders/{elderId}", async (string elderId, ElderProfileUpdateRequest request, HttpContext context, IHttpClientFactory httpClientFactory, IConfiguration configuration, CancellationToken cancellationToken) =>
{
	try
	{
		var client = httpClientFactory.CreateClient();
		var response = await PutJsonAsync<ElderProfileSummaryResponse>(client, context, $"{ResolveServiceUrl(configuration, "Elder", "http://localhost:5062")}/api/elders/{Uri.EscapeDataString(elderId)}", request, cancellationToken);
		return Results.Ok(response);
	}
	catch (Exception ex) { return Results.Problem(title: "长者主档更新失败。", detail: ex.Message, statusCode: StatusCodes.Status502BadGateway); }
}).RequireAuthorization();

app.MapGet("/api/admin/elders/face-enrollment", async (HttpContext context, IHttpClientFactory httpClientFactory, IConfiguration configuration, CancellationToken cancellationToken, string? keyword, string? status, int page = 1, int pageSize = 50) =>
{
	try
	{
		var client = httpClientFactory.CreateClient();
		var qs = $"?page={page}&pageSize={pageSize}";
		if (!string.IsNullOrWhiteSpace(keyword)) qs += $"&keyword={Uri.EscapeDataString(keyword)}";
		if (!string.IsNullOrWhiteSpace(status)) qs += $"&status={Uri.EscapeDataString(status)}";
		var response = await GetJsonAsync<ElderFaceEnrollmentListResponse>(client, context, $"{ResolveServiceUrl(configuration, "Elder", "http://localhost:5062")}/api/elders/face-enrollment{qs}", cancellationToken);
		return Results.Ok(response);
	}
	catch (Exception ex) { return Results.Problem(title: "人脸录入队列查询失败。", detail: ex.Message, statusCode: StatusCodes.Status502BadGateway); }
}).RequireAuthorization();

app.MapPost("/api/admin/elders/{elderId}/face-enrollment/start", async (string elderId, ElderFaceEnrollmentUpdateRequest request, HttpContext context, IHttpClientFactory httpClientFactory, IConfiguration configuration, CancellationToken cancellationToken) =>
{
	try
	{
		var client = httpClientFactory.CreateClient();
		var response = await PostJsonAsync<ElderFaceEnrollmentListItemResponse>(client, context, $"{ResolveServiceUrl(configuration, "Elder", "http://localhost:5062")}/api/elders/{Uri.EscapeDataString(elderId)}/face-enrollment/start", request, cancellationToken);
		return Results.Ok(response);
	}
	catch (Exception ex) { return Results.Problem(title: "开始人脸采集失败。", detail: ex.Message, statusCode: StatusCodes.Status502BadGateway); }
}).RequireAuthorization();

app.MapPost("/api/admin/elders/{elderId}/face-enrollment/capture", async (string elderId, ElderFaceCaptureRequest request, HttpContext context, IHttpClientFactory httpClientFactory, IConfiguration configuration, CancellationToken cancellationToken) =>
{
	try
	{
		var client = httpClientFactory.CreateClient();
		var response = await PostJsonAsync<ElderFaceEnrollmentListItemResponse>(client, context, $"{ResolveServiceUrl(configuration, "Elder", "http://localhost:5062")}/api/elders/{Uri.EscapeDataString(elderId)}/face-enrollment/capture", request, cancellationToken);
		return Results.Ok(response);
	}
	catch (Exception ex) { return Results.Problem(title: "采集人脸样本失败。", detail: ex.Message, statusCode: StatusCodes.Status502BadGateway); }
}).RequireAuthorization();

app.MapPost("/api/admin/elders/{elderId}/face-enrollment/activate", async (string elderId, ElderFaceActivationRequest request, HttpContext context, IHttpClientFactory httpClientFactory, IConfiguration configuration, CancellationToken cancellationToken) =>
{
	try
	{
		var client = httpClientFactory.CreateClient();
		var response = await PostJsonAsync<ElderFaceEnrollmentListItemResponse>(client, context, $"{ResolveServiceUrl(configuration, "Elder", "http://localhost:5062")}/api/elders/{Uri.EscapeDataString(elderId)}/face-enrollment/activate", request, cancellationToken);
		return Results.Ok(response);
	}
	catch (Exception ex) { return Results.Problem(title: "激活人脸模板失败。", detail: ex.Message, statusCode: StatusCodes.Status502BadGateway); }
}).RequireAuthorization();

app.MapPost("/api/admin/elders/{elderId}/face-enrollment/retake", async (string elderId, ElderFaceRetakeRequest request, HttpContext context, IHttpClientFactory httpClientFactory, IConfiguration configuration, CancellationToken cancellationToken) =>
{
	try
	{
		var client = httpClientFactory.CreateClient();
		var response = await PostJsonAsync<ElderFaceEnrollmentListItemResponse>(client, context, $"{ResolveServiceUrl(configuration, "Elder", "http://localhost:5062")}/api/elders/{Uri.EscapeDataString(elderId)}/face-enrollment/retake", request, cancellationToken);
		return Results.Ok(response);
	}
	catch (Exception ex) { return Results.Problem(title: "退回人脸重录失败。", detail: ex.Message, statusCode: StatusCodes.Status502BadGateway); }
}).RequireAuthorization();

// ── Health Service Proxy ───────────────────────────────────────────────────

app.MapGet("/api/admin/elders/{elderId}/health-summary", async (string elderId, HttpContext context, IHttpClientFactory httpClientFactory, IConfiguration configuration, CancellationToken cancellationToken) =>
{
	try
	{
		var client = httpClientFactory.CreateClient();
		var response = await GetJsonAsync<HealthArchiveSummaryResponse>(client, context, $"{ResolveServiceUrl(configuration, "Health", "http://localhost:5197")}/api/health/elders/{Uri.EscapeDataString(elderId)}/summary", cancellationToken);
		return Results.Ok(response);
	}
	catch (Exception ex) { return Results.Problem(title: "健康摘要查询失败。", detail: ex.Message, statusCode: StatusCodes.Status502BadGateway); }
}).RequireAuthorization();

app.MapGet("/api/admin/health/archives", async (HttpContext context, IHttpClientFactory httpClientFactory, IConfiguration configuration, CancellationToken cancellationToken, string? keyword) =>
{
	try
	{
		var client = httpClientFactory.CreateClient();
		var healthTask = GetJsonAsync<HealthArchiveListResponse>(
			client,
			context,
			$"{ResolveServiceUrl(configuration, "Health", "http://localhost:5197")}/api/health/archives",
			cancellationToken);
		var elderTask = GetJsonAsync<ElderListResponse>(
			client,
			context,
			$"{ResolveServiceUrl(configuration, "Elder", "http://localhost:5062")}/api/elders?page=1&pageSize=500",
			cancellationToken);

		await Task.WhenAll(healthTask, elderTask);

		var healthResponse = await healthTask;
		var elderResponse = await elderTask;
		var elderLookup = elderResponse.Items.ToDictionary(item => item.ElderId, StringComparer.Ordinal);

		var items = healthResponse.Items
			.Select(item =>
			{
				if (elderLookup.TryGetValue(item.ElderId, out var elder))
				{
					return new AdminHealthArchiveListItemResponse(
						ElderId: item.ElderId,
						TenantId: item.TenantId,
						ElderName: item.ElderName,
						RoomNumber: elder.RoomNumber,
						Age: elder.Age,
						CareLevel: elder.CareLevel,
						AdmissionStatus: elder.AdmissionStatus,
						BloodPressure: item.BloodPressure,
						HeartRate: item.HeartRate,
						Temperature: item.Temperature,
						BloodSugar: item.BloodSugar,
						Oxygen: item.Oxygen,
						RiskSummary: item.RiskSummary,
						UpdatedAtUtc: item.UpdatedAtUtc);
				}

				return new AdminHealthArchiveListItemResponse(
					ElderId: item.ElderId,
					TenantId: item.TenantId,
					ElderName: item.ElderName,
					RoomNumber: "待补录",
					Age: 0,
					CareLevel: "Unknown",
					AdmissionStatus: "Unknown",
					BloodPressure: item.BloodPressure,
					HeartRate: item.HeartRate,
					Temperature: item.Temperature,
					BloodSugar: item.BloodSugar,
					Oxygen: item.Oxygen,
					RiskSummary: item.RiskSummary,
					UpdatedAtUtc: item.UpdatedAtUtc);
			})
			.Where(item => string.IsNullOrWhiteSpace(keyword)
				|| item.ElderName.Contains(keyword, StringComparison.OrdinalIgnoreCase)
				|| item.RoomNumber.Contains(keyword, StringComparison.OrdinalIgnoreCase))
			.OrderByDescending(item => item.UpdatedAtUtc)
			.ToArray();

		return Results.Ok(new AdminHealthArchiveListResponse(items, healthResponse.GeneratedAtUtc));
	}
	catch (Exception ex) { return Results.Problem(title: "健康档案列表查询失败。", detail: ex.Message, statusCode: StatusCodes.Status502BadGateway); }
}).RequireAuthorization();

app.MapPost("/api/admin/health/archives", async (AdminHealthArchiveCreateRequest request, HttpContext context, IHttpClientFactory httpClientFactory, IConfiguration configuration, CancellationToken cancellationToken) =>
{
	try
	{
		var client = httpClientFactory.CreateClient();
		var elder = await GetJsonAsync<ElderProfileSummaryResponse>(
			client,
			context,
			$"{ResolveServiceUrl(configuration, "Elder", "http://localhost:5062")}/api/elders/{Uri.EscapeDataString(request.ElderId)}",
			cancellationToken);
		var health = await PostJsonAsync<HealthArchiveSummaryResponse>(
			client,
			context,
			$"{ResolveServiceUrl(configuration, "Health", "http://localhost:5197")}/api/health/archives",
			new HealthArchiveCreateRequest(
				ElderId: request.ElderId,
				ElderName: elder.ElderName,
				BloodPressure: request.BloodPressure,
				HeartRate: request.HeartRate,
				Temperature: request.Temperature,
				BloodSugar: request.BloodSugar,
				Oxygen: request.Oxygen,
				RiskSummary: string.IsNullOrWhiteSpace(request.RiskSummary) ? "需持续观察" : request.RiskSummary),
			cancellationToken);

		return Results.Ok(new AdminHealthArchiveListItemResponse(
			ElderId: health.ElderId,
			TenantId: health.TenantId,
			ElderName: health.ElderName,
			RoomNumber: elder.RoomNumber,
			Age: elder.Age,
			CareLevel: elder.CareLevel,
			AdmissionStatus: elder.AdmissionStatus,
			BloodPressure: health.BloodPressure,
			HeartRate: health.HeartRate,
			Temperature: health.Temperature,
			BloodSugar: health.BloodSugar,
			Oxygen: health.Oxygen,
			RiskSummary: health.RiskSummary,
			UpdatedAtUtc: health.UpdatedAtUtc));
	}
	catch (Exception ex) { return Results.Problem(title: "健康档案建档失败。", detail: ex.Message, statusCode: StatusCodes.Status502BadGateway); }
}).RequireAuthorization();

app.MapGet("/api/admin/vitals", async (HttpContext context, IHttpClientFactory httpClientFactory, IConfiguration configuration, CancellationToken cancellationToken, int? take, string? elderId, string? keyword) =>
{
	try
	{
		var client = httpClientFactory.CreateClient();
		var query = new List<string>();
		if (take is int t) query.Add($"take={t}");
		if (!string.IsNullOrWhiteSpace(elderId)) query.Add($"elderId={Uri.EscapeDataString(elderId)}");
		var qs = query.Count > 0 ? $"?{string.Join("&", query)}" : string.Empty;

		var vitalsTask = GetJsonAsync<List<VitalObservationResponse>>(
			client,
			context,
			$"{ResolveServiceUrl(configuration, "Health", "http://localhost:5197")}/api/health/vitals{qs}",
			cancellationToken);
		var elderTask = GetJsonAsync<ElderListResponse>(
			client,
			context,
			$"{ResolveServiceUrl(configuration, "Elder", "http://localhost:5062")}/api/elders?page=1&pageSize=500",
			cancellationToken);

		await Task.WhenAll(vitalsTask, elderTask);
		var vitals = await vitalsTask;
		var elderLookup = (await elderTask).Items.ToDictionary(item => item.ElderId, StringComparer.Ordinal);

		var items = vitals
			.Select(item =>
			{
				elderLookup.TryGetValue(item.ElderId, out var elder);
				return new AdminVitalObservationResponse(
					ObservationId: item.ObservationId,
					TenantId: item.TenantId,
					ElderId: item.ElderId,
					ElderName: elder?.ElderName ?? item.ElderId,
					RoomNumber: elder?.RoomNumber ?? "待补录",
					BloodPressure: item.BloodPressure,
					HeartRate: item.HeartRate,
					Temperature: item.Temperature,
					BloodSugar: item.BloodSugar,
					Oxygen: item.Oxygen,
					RecordedBy: item.RecordedBy,
					RecordedAtUtc: item.RecordedAtUtc);
			})
			.Where(item => string.IsNullOrWhiteSpace(keyword)
				|| item.ElderName.Contains(keyword, StringComparison.OrdinalIgnoreCase)
				|| item.RoomNumber.Contains(keyword, StringComparison.OrdinalIgnoreCase)
				|| item.ElderId.Contains(keyword, StringComparison.OrdinalIgnoreCase))
			.OrderByDescending(item => item.RecordedAtUtc)
			.ToArray();

		return Results.Ok(items);
	}
	catch (Exception ex) { return Results.Problem(title: "体征记录查询失败。", detail: ex.Message, statusCode: StatusCodes.Status502BadGateway); }
}).RequireAuthorization();

app.MapPost("/api/admin/vitals", async (VitalObservationCreateRequest request, HttpContext context, IHttpClientFactory httpClientFactory, IConfiguration configuration, CancellationToken cancellationToken) =>
{
	try
	{
		var client = httpClientFactory.CreateClient();
		var elder = await GetJsonAsync<ElderProfileSummaryResponse>(
			client,
			context,
			$"{ResolveServiceUrl(configuration, "Elder", "http://localhost:5062")}/api/elders/{Uri.EscapeDataString(request.ElderId)}",
			cancellationToken);
		var response = await PostJsonAsync<VitalObservationResponse>(
			client,
			context,
			$"{ResolveServiceUrl(configuration, "Health", "http://localhost:5197")}/api/health/vitals",
			request,
			cancellationToken);

		return Results.Ok(new AdminVitalObservationResponse(
			ObservationId: response.ObservationId,
			TenantId: response.TenantId,
			ElderId: response.ElderId,
			ElderName: elder.ElderName,
			RoomNumber: elder.RoomNumber,
			BloodPressure: response.BloodPressure,
			HeartRate: response.HeartRate,
			Temperature: response.Temperature,
			BloodSugar: response.BloodSugar,
			Oxygen: response.Oxygen,
			RecordedBy: response.RecordedBy,
			RecordedAtUtc: response.RecordedAtUtc));
	}
	catch (Exception ex) { return Results.Problem(title: "体征记录写入失败。", detail: ex.Message, statusCode: StatusCodes.Status502BadGateway); }
}).RequireAuthorization();

// ── Billing Service Proxy ──────────────────────────────────────────────────

app.MapGet("/api/admin/finance/summary", async (HttpContext context, IHttpClientFactory httpClientFactory, IConfiguration configuration, CancellationToken cancellationToken) =>
{
	try
	{
		var client = httpClientFactory.CreateClient();
		var response = await GetJsonAsync<AdminFinanceSummaryResponse>(client, context, $"{ResolveServiceUrl(configuration, "Billing", "http://localhost:5253")}/api/billing/summary", cancellationToken);
		return Results.Ok(response);
	}
	catch (Exception ex) { return Results.Problem(title: "财务 summary 查询失败。", detail: ex.Message, statusCode: StatusCodes.Status502BadGateway); }
}).RequireAuthorization();

app.MapGet("/api/admin/finance/invoices", async (HttpContext context, IHttpClientFactory httpClientFactory, IConfiguration configuration, CancellationToken cancellationToken, string? status, string? notificationStatus) =>
{
	try
	{
		var client = httpClientFactory.CreateClient();
		var query = new List<string>();
		if (!string.IsNullOrWhiteSpace(status)) query.Add($"status={Uri.EscapeDataString(status)}");
		if (!string.IsNullOrWhiteSpace(notificationStatus)) query.Add($"notificationStatus={Uri.EscapeDataString(notificationStatus)}");
		var qs = query.Count > 0 ? $"?{string.Join("&", query)}" : string.Empty;
		var response = await GetJsonAsync<List<BillingInvoiceResponse>>(client, context, $"{ResolveServiceUrl(configuration, "Billing", "http://localhost:5253")}/api/billing/invoices{qs}", cancellationToken);
		return Results.Ok(response);
	}
	catch (Exception ex) { return Results.Problem(title: "财务发票队列查询失败。", detail: ex.Message, statusCode: StatusCodes.Status502BadGateway); }
}).RequireAuthorization();

app.MapPost("/api/admin/finance/invoices", async (BillingInvoiceCreateRequest request, HttpContext context, IHttpClientFactory httpClientFactory, IConfiguration configuration, CancellationToken cancellationToken) =>
{
	try
	{
		var client = httpClientFactory.CreateClient();
		var response = await PostJsonAsync<BillingInvoiceResponse>(client, context, $"{ResolveServiceUrl(configuration, "Billing", "http://localhost:5253")}/api/billing/invoices", request, cancellationToken);
		return Results.Ok(response);
	}
	catch (Exception ex) { return Results.Problem(title: "财务发票创建失败。", detail: ex.Message, statusCode: StatusCodes.Status502BadGateway); }
}).RequireAuthorization();

app.MapGet("/api/admin/elders/{elderId}/invoices", async (string elderId, HttpContext context, IHttpClientFactory httpClientFactory, IConfiguration configuration, CancellationToken cancellationToken) =>
{
	try
	{
		var client = httpClientFactory.CreateClient();
		var response = await GetJsonAsync<List<BillingInvoiceResponse>>(client, context, $"{ResolveServiceUrl(configuration, "Billing", "http://localhost:5253")}/api/billing/elders/{Uri.EscapeDataString(elderId)}/invoices", cancellationToken);
		return Results.Ok(response);
	}
	catch (Exception ex) { return Results.Problem(title: "账单列表查询失败。", detail: ex.Message, statusCode: StatusCodes.Status502BadGateway); }
}).RequireAuthorization();

app.MapGet("/api/admin/invoices/{invoiceId}", async (string invoiceId, HttpContext context, IHttpClientFactory httpClientFactory, IConfiguration configuration, CancellationToken cancellationToken) =>
{
	try
	{
		var client = httpClientFactory.CreateClient();
		var response = await GetJsonAsync<BillingInvoiceResponse>(client, context, $"{ResolveServiceUrl(configuration, "Billing", "http://localhost:5253")}/api/billing/invoices/{Uri.EscapeDataString(invoiceId)}", cancellationToken);
		return Results.Ok(response);
	}
	catch (Exception ex) { return Results.Problem(title: "账单详情查询失败。", detail: ex.Message, statusCode: StatusCodes.Status502BadGateway); }
}).RequireAuthorization();

app.MapGet("/api/admin/billing/observability", async (HttpContext context, IHttpClientFactory httpClientFactory, IConfiguration configuration, CancellationToken cancellationToken) =>
{
	try
	{
		var client = httpClientFactory.CreateClient();
		var response = await GetJsonAsync<BillingObservabilityResponse>(client, context, $"{ResolveServiceUrl(configuration, "Billing", "http://localhost:5253")}/api/billing/observability", cancellationToken);
		return Results.Ok(response);
	}
	catch (Exception ex) { return Results.Problem(title: "账单可观测数据查询失败。", detail: ex.Message, statusCode: StatusCodes.Status502BadGateway); }
}).RequireAuthorization();

// ── Staffing Service Proxy ────────────────────────────────────────────────

app.MapGet("/api/admin/staff", async (HttpContext context, IHttpClientFactory httpClientFactory, IConfiguration configuration, CancellationToken cancellationToken, string? keyword, string? department, string? employmentSource, string? status, string? lifecycleStatus, string? partnerAgency, int? page, int? pageSize) =>
{
	try
	{
		var client = httpClientFactory.CreateClient();
		var query = new List<string>();
		if (!string.IsNullOrWhiteSpace(keyword)) query.Add($"keyword={Uri.EscapeDataString(keyword)}");
		if (!string.IsNullOrWhiteSpace(department)) query.Add($"department={Uri.EscapeDataString(department)}");
		if (!string.IsNullOrWhiteSpace(employmentSource)) query.Add($"employmentSource={Uri.EscapeDataString(employmentSource)}");
		if (!string.IsNullOrWhiteSpace(status)) query.Add($"status={Uri.EscapeDataString(status)}");
		if (!string.IsNullOrWhiteSpace(lifecycleStatus)) query.Add($"lifecycleStatus={Uri.EscapeDataString(lifecycleStatus)}");
		if (!string.IsNullOrWhiteSpace(partnerAgency)) query.Add($"partnerAgency={Uri.EscapeDataString(partnerAgency)}");
		if (page is > 0) query.Add($"page={page.Value}");
		if (pageSize is > 0) query.Add($"pageSize={pageSize.Value}");
		var qs = query.Count > 0 ? $"?{string.Join("&", query)}" : string.Empty;
		var response = await GetJsonAsync<AdminStaffListResponse>(client, context, $"{ResolveServiceUrl(configuration, "Staffing", "http://localhost:5216")}/api/staffing/staff{qs}", cancellationToken);
		return Results.Ok(response);
	}
	catch (Exception ex) { return Results.Problem(title: "员工列表查询失败。", detail: ex.Message, statusCode: StatusCodes.Status502BadGateway); }
}).RequireAuthorization();

app.MapGet("/api/admin/staff/{staffId}", async (string staffId, HttpContext context, IHttpClientFactory httpClientFactory, IConfiguration configuration, CancellationToken cancellationToken) =>
{
	try
	{
		var client = httpClientFactory.CreateClient();
		var response = await GetJsonAsync<AdminStaffRecordResponse>(client, context, $"{ResolveServiceUrl(configuration, "Staffing", "http://localhost:5216")}/api/staffing/staff/{Uri.EscapeDataString(staffId)}", cancellationToken);
		return Results.Ok(response);
	}
	catch (Exception ex) { return Results.Problem(title: "员工详情查询失败。", detail: ex.Message, statusCode: StatusCodes.Status502BadGateway); }
}).RequireAuthorization();

app.MapPost("/api/admin/staff", async (AdminStaffCreateRequest request, HttpContext context, IHttpClientFactory httpClientFactory, IConfiguration configuration, CancellationToken cancellationToken) =>
{
	try
	{
		var client = httpClientFactory.CreateClient();
		var response = await PostJsonAsync<AdminStaffRecordResponse>(client, context, $"{ResolveServiceUrl(configuration, "Staffing", "http://localhost:5216")}/api/staffing/staff", request, cancellationToken);
		return Results.Ok(response);
	}
	catch (Exception ex) { return Results.Problem(title: "员工建档失败。", detail: ex.Message, statusCode: StatusCodes.Status502BadGateway); }
}).RequireAuthorization();

app.MapPost("/api/admin/staff/{staffId}/activate", async (string staffId, AdminStaffActivateRequest request, HttpContext context, IHttpClientFactory httpClientFactory, IConfiguration configuration, CancellationToken cancellationToken) =>
{
	try
	{
		var client = httpClientFactory.CreateClient();
		var response = await PostJsonAsync<AdminStaffRecordResponse>(client, context, $"{ResolveServiceUrl(configuration, "Staffing", "http://localhost:5216")}/api/staffing/staff/{Uri.EscapeDataString(staffId)}/activate", request, cancellationToken);
		return Results.Ok(response);
	}
	catch (Exception ex) { return Results.Problem(title: "员工确认入职失败。", detail: ex.Message, statusCode: StatusCodes.Status502BadGateway); }
}).RequireAuthorization();

// ── Rooms Service Proxy ───────────────────────────────────────────────────

app.MapGet("/api/admin/organizations", async (HttpContext context, IHttpClientFactory httpClientFactory, IConfiguration configuration, CancellationToken cancellationToken, string? keyword, string? status, string? lifecycleStatus, int? page, int? pageSize) =>
{
	try
	{
		var client = httpClientFactory.CreateClient();
		var query = new List<string>();
		if (!string.IsNullOrWhiteSpace(keyword)) query.Add($"keyword={Uri.EscapeDataString(keyword)}");
		if (!string.IsNullOrWhiteSpace(status)) query.Add($"status={Uri.EscapeDataString(status)}");
		if (!string.IsNullOrWhiteSpace(lifecycleStatus)) query.Add($"lifecycleStatus={Uri.EscapeDataString(lifecycleStatus)}");
		if (page is > 0) query.Add($"page={page.Value}");
		if (pageSize is > 0) query.Add($"pageSize={pageSize.Value}");
		var qs = query.Count > 0 ? $"?{string.Join("&", query)}" : string.Empty;

		var organizationsTask = GetJsonAsync<OrganizationListResponse>(client, context, $"{ResolveServiceUrl(configuration, "Organization", "http://localhost:5218")}/api/organizations/organizations{qs}", cancellationToken);
		var roomsTask = FetchAllRoomsAsync(client, context, configuration, cancellationToken);
		var eldersTask = FetchAllEldersAsync(client, context, configuration, cancellationToken);
		var staffTask = FetchAllStaffAsync(client, context, configuration, cancellationToken);

		await Task.WhenAll(organizationsTask, roomsTask, eldersTask, staffTask);
		var organizations = await organizationsTask;
		var rooms = await roomsTask;
		var elders = await eldersTask;
		var staff = await staffTask;
		var mergedRooms = rooms.Select(room => MergeRoomRecord(room, elders)).ToArray();
		var items = organizations.Items.Select(item => MergeOrganizationSummary(item, mergedRooms, staff)).ToArray();

		return Results.Ok(new AdminOrganizationListResponse(items, organizations.Total, organizations.Page, organizations.PageSize));
	}
	catch (Exception ex) { return Results.Problem(title: "机构列表查询失败。", detail: ex.Message, statusCode: StatusCodes.Status502BadGateway); }
}).RequireAuthorization();

app.MapGet("/api/admin/organizations/{organizationId}", async (string organizationId, HttpContext context, IHttpClientFactory httpClientFactory, IConfiguration configuration, CancellationToken cancellationToken) =>
{
	try
	{
		var client = httpClientFactory.CreateClient();
		var organizationTask = GetJsonAsync<OrganizationRecordResponse>(client, context, $"{ResolveServiceUrl(configuration, "Organization", "http://localhost:5218")}/api/organizations/organizations/{Uri.EscapeDataString(organizationId)}", cancellationToken);
		var roomsTask = FetchAllRoomsAsync(client, context, configuration, cancellationToken);
		var eldersTask = FetchAllEldersAsync(client, context, configuration, cancellationToken);
		var staffTask = FetchAllStaffAsync(client, context, configuration, cancellationToken, organizationId);

		await Task.WhenAll(organizationTask, roomsTask, eldersTask, staffTask);
		var organization = await organizationTask;
		var rooms = await roomsTask;
		var elders = await eldersTask;
		var staff = await staffTask;
		var mergedRooms = rooms.Select(room => MergeRoomRecord(room, elders)).Where(room => MatchesOrganization(room, organization)).OrderBy(room => room.Floor).ThenBy(room => room.RoomId).ToArray();
		var matchedStaff = staff.Where(item => MatchesStaffOrganization(item, organization)).OrderBy(item => item.LifecycleStatus == "待入职" ? 0 : item.Status == "休假" ? 1 : 2).ThenBy(item => item.Name).ToArray();
		var summary = MergeOrganizationSummary(organization, mergedRooms, matchedStaff);
		var detail = new AdminOrganizationDetailResponse(
			Organization: summary,
			Rooms: mergedRooms.Select(MapOrganizationRoom).ToArray(),
			Staff: matchedStaff);

		return Results.Ok(detail);
	}
	catch (Exception ex) { return Results.Problem(title: "机构详情查询失败。", detail: ex.Message, statusCode: StatusCodes.Status502BadGateway); }
}).RequireAuthorization();

app.MapPost("/api/admin/organizations", async (OrganizationCreateRequest request, HttpContext context, IHttpClientFactory httpClientFactory, IConfiguration configuration, CancellationToken cancellationToken) =>
{
	try
	{
		var client = httpClientFactory.CreateClient();
		var response = await PostJsonAsync<OrganizationRecordResponse>(client, context, $"{ResolveServiceUrl(configuration, "Organization", "http://localhost:5218")}/api/organizations/organizations", request, cancellationToken);
		return Results.Ok(MergeOrganizationSummary(response, [], []));
	}
	catch (Exception ex) { return Results.Problem(title: "机构建档失败。", detail: ex.Message, statusCode: StatusCodes.Status502BadGateway); }
}).RequireAuthorization();

app.MapPost("/api/admin/organizations/{organizationId}/activate", async (string organizationId, OrganizationActivateRequest request, HttpContext context, IHttpClientFactory httpClientFactory, IConfiguration configuration, CancellationToken cancellationToken) =>
{
	try
	{
		var client = httpClientFactory.CreateClient();
		var organizationTask = PostJsonAsync<OrganizationRecordResponse>(client, context, $"{ResolveServiceUrl(configuration, "Organization", "http://localhost:5218")}/api/organizations/organizations/{Uri.EscapeDataString(organizationId)}/activate", request, cancellationToken);
		var roomsTask = FetchAllRoomsAsync(client, context, configuration, cancellationToken);
		var eldersTask = FetchAllEldersAsync(client, context, configuration, cancellationToken);
		var staffTask = FetchAllStaffAsync(client, context, configuration, cancellationToken, organizationId);

		await Task.WhenAll(organizationTask, roomsTask, eldersTask, staffTask);
		var organization = await organizationTask;
		var rooms = await roomsTask;
		var elders = await eldersTask;
		var staff = await staffTask;
		var mergedRooms = rooms.Select(room => MergeRoomRecord(room, elders)).Where(room => MatchesOrganization(room, organization)).ToArray();
		return Results.Ok(MergeOrganizationSummary(organization, mergedRooms, staff));
	}
	catch (Exception ex) { return Results.Problem(title: "机构启用失败。", detail: ex.Message, statusCode: StatusCodes.Status502BadGateway); }
}).RequireAuthorization();

app.MapGet("/api/admin/rooms", async (HttpContext context, IHttpClientFactory httpClientFactory, IConfiguration configuration, CancellationToken cancellationToken, string? keyword, string? status, string? lifecycleStatus, string? organizationName, int? page, int? pageSize) =>
{
	try
	{
		var client = httpClientFactory.CreateClient();
		var query = new List<string>();
		if (!string.IsNullOrWhiteSpace(keyword)) query.Add($"keyword={Uri.EscapeDataString(keyword)}");
		if (!string.IsNullOrWhiteSpace(status)) query.Add($"status={Uri.EscapeDataString(status)}");
		if (!string.IsNullOrWhiteSpace(lifecycleStatus)) query.Add($"lifecycleStatus={Uri.EscapeDataString(lifecycleStatus)}");
		if (!string.IsNullOrWhiteSpace(organizationName)) query.Add($"organizationName={Uri.EscapeDataString(organizationName)}");
		if (page is > 0) query.Add($"page={page.Value}");
		if (pageSize is > 0) query.Add($"pageSize={pageSize.Value}");
		var qs = query.Count > 0 ? $"?{string.Join("&", query)}" : string.Empty;

		var roomsTask = GetJsonAsync<AdminRoomListResponse>(client, context, $"{ResolveServiceUrl(configuration, "Rooms", "http://localhost:5217")}/api/rooms/rooms{qs}", cancellationToken);
		var eldersTask = FetchAllEldersAsync(client, context, configuration, cancellationToken);

		await Task.WhenAll(roomsTask, eldersTask);
		var rooms = await roomsTask;
		var elders = await eldersTask;
		var mergedItems = rooms.Items.Select(room => MergeRoomRecord(room, elders)).ToArray();
		return Results.Ok(new AdminRoomListResponse(mergedItems, rooms.Total, rooms.Page, rooms.PageSize));
	}
	catch (Exception ex) { return Results.Problem(title: "房间列表查询失败。", detail: ex.Message, statusCode: StatusCodes.Status502BadGateway); }
}).RequireAuthorization();

app.MapGet("/api/admin/rooms/{roomId}", async (string roomId, HttpContext context, IHttpClientFactory httpClientFactory, IConfiguration configuration, CancellationToken cancellationToken) =>
{
	try
	{
		var client = httpClientFactory.CreateClient();
		var roomTask = GetJsonAsync<AdminRoomRecordResponse>(client, context, $"{ResolveServiceUrl(configuration, "Rooms", "http://localhost:5217")}/api/rooms/rooms/{Uri.EscapeDataString(roomId)}", cancellationToken);
		var eldersTask = FetchAllEldersAsync(client, context, configuration, cancellationToken);

		await Task.WhenAll(roomTask, eldersTask);
		var room = await roomTask;
		var elders = await eldersTask;
		return Results.Ok(MergeRoomRecord(room, elders));
	}
	catch (Exception ex) { return Results.Problem(title: "房间详情查询失败。", detail: ex.Message, statusCode: StatusCodes.Status502BadGateway); }
}).RequireAuthorization();

app.MapPost("/api/admin/rooms", async (AdminRoomCreateRequest request, HttpContext context, IHttpClientFactory httpClientFactory, IConfiguration configuration, CancellationToken cancellationToken) =>
{
	try
	{
		var client = httpClientFactory.CreateClient();
		var response = await PostJsonAsync<AdminRoomRecordResponse>(client, context, $"{ResolveServiceUrl(configuration, "Rooms", "http://localhost:5217")}/api/rooms/rooms", request, cancellationToken);
		return Results.Ok(response);
	}
	catch (Exception ex) { return Results.Problem(title: "房间建档失败。", detail: ex.Message, statusCode: StatusCodes.Status502BadGateway); }
}).RequireAuthorization();

app.MapPost("/api/admin/rooms/{roomId}/activate", async (string roomId, AdminRoomActivateRequest request, HttpContext context, IHttpClientFactory httpClientFactory, IConfiguration configuration, CancellationToken cancellationToken) =>
{
	try
	{
		var client = httpClientFactory.CreateClient();
		var response = await PostJsonAsync<AdminRoomRecordResponse>(client, context, $"{ResolveServiceUrl(configuration, "Rooms", "http://localhost:5217")}/api/rooms/rooms/{Uri.EscapeDataString(roomId)}/activate", request, cancellationToken);
		var elders = await FetchAllEldersAsync(client, context, configuration, cancellationToken);
		return Results.Ok(MergeRoomRecord(response, elders));
	}
	catch (Exception ex) { return Results.Problem(title: "房间启用失败。", detail: ex.Message, statusCode: StatusCodes.Status502BadGateway); }
}).RequireAuthorization();

// ── Visit Service Proxy ────────────────────────────────────────────────────

app.MapGet("/api/admin/elders/{elderId}/appointments", async (string elderId, HttpContext context, IHttpClientFactory httpClientFactory, IConfiguration configuration, CancellationToken cancellationToken) =>
{
	try
	{
		var client = httpClientFactory.CreateClient();
		var response = await GetJsonAsync<List<VisitAppointmentResponse>>(client, context, $"{ResolveServiceUrl(configuration, "Visit", "http://localhost:5050")}/api/visits/elders/{Uri.EscapeDataString(elderId)}/appointments", cancellationToken);
		return Results.Ok(response);
	}
	catch (Exception ex) { return Results.Problem(title: "探访预约查询失败。", detail: ex.Message, statusCode: StatusCodes.Status502BadGateway); }
}).RequireAuthorization();

app.MapGet("/api/admin/visits", async (HttpContext context, IHttpClientFactory httpClientFactory, IConfiguration configuration, CancellationToken cancellationToken, int? take) =>
{
	try
	{
		var client = httpClientFactory.CreateClient();
		var qs = take.HasValue ? $"?take={take.Value}" : string.Empty;
		var response = await GetJsonAsync<List<AdminVisitAppointmentResponse>>(client, context, $"{ResolveServiceUrl(configuration, "Visit", "http://localhost:5050")}/api/visits/appointments{qs}", cancellationToken);
		return Results.Ok(response);
	}
	catch (Exception ex) { return Results.Problem(title: "探视列表查询失败。", detail: ex.Message, statusCode: StatusCodes.Status502BadGateway); }
}).RequireAuthorization();

app.MapPost("/api/admin/visits", async (HttpContext context, VisitAppointmentCreateRequest request, IHttpClientFactory httpClientFactory, IConfiguration configuration, CancellationToken cancellationToken) =>
{
	try
	{
		var client = httpClientFactory.CreateClient();
		var response = await PostJsonAsync<VisitAppointmentResponse>(client, context, $"{ResolveServiceUrl(configuration, "Visit", "http://localhost:5050")}/api/visits/appointments", request, cancellationToken);
		return Results.Ok(response);
	}
	catch (Exception ex) { return Results.Problem(title: "探视预约创建失败。", detail: ex.Message, statusCode: StatusCodes.Status502BadGateway); }
}).RequireAuthorization();

// ── Notification Service Proxy ─────────────────────────────────────────────

app.MapGet("/api/admin/notifications/summary", async (HttpContext context, IHttpClientFactory httpClientFactory, IConfiguration configuration, CancellationToken cancellationToken) =>
{
	try
	{
		var client = httpClientFactory.CreateClient();
		var response = await GetJsonAsync<AdminNotificationSummaryResponse>(client, context, $"{ResolveServiceUrl(configuration, "Notification", "http://localhost:5144")}/api/notifications/summary", cancellationToken);
		return Results.Ok(response);
	}
	catch (Exception ex) { return Results.Problem(title: "通知 summary 查询失败。", detail: ex.Message, statusCode: StatusCodes.Status502BadGateway); }
}).RequireAuthorization();

app.MapGet("/api/admin/notifications/queue", async (HttpContext context, IHttpClientFactory httpClientFactory, IConfiguration configuration, CancellationToken cancellationToken, string? category, string? status) =>
{
	try
	{
		var client = httpClientFactory.CreateClient();
		var query = new List<string>();
		if (!string.IsNullOrWhiteSpace(category)) query.Add($"category={Uri.EscapeDataString(category)}");
		if (!string.IsNullOrWhiteSpace(status)) query.Add($"status={Uri.EscapeDataString(status)}");
		var qs = query.Count > 0 ? $"?{string.Join("&", query)}" : string.Empty;
		var response = await GetJsonAsync<List<NotificationMessageResponse>>(client, context, $"{ResolveServiceUrl(configuration, "Notification", "http://localhost:5144")}/api/notifications{qs}", cancellationToken);
		return Results.Ok(response);
	}
	catch (Exception ex) { return Results.Problem(title: "通知队列查询失败。", detail: ex.Message, statusCode: StatusCodes.Status502BadGateway); }
}).RequireAuthorization();

app.MapGet("/api/admin/notifications", async (HttpContext context, IHttpClientFactory httpClientFactory, IConfiguration configuration, CancellationToken cancellationToken, string? audience, string? audienceKey) =>
{
	try
	{
		var client = httpClientFactory.CreateClient();
		var qs = "";
		if (!string.IsNullOrWhiteSpace(audience)) qs += $"?audience={Uri.EscapeDataString(audience)}";
		if (!string.IsNullOrWhiteSpace(audienceKey)) qs += $"{(qs.Length > 0 ? "&" : "?")}audienceKey={Uri.EscapeDataString(audienceKey)}";
		var response = await GetJsonAsync<List<NotificationMessageResponse>>(client, context, $"{ResolveServiceUrl(configuration, "Notification", "http://localhost:5144")}/api/notifications{qs}", cancellationToken);
		return Results.Ok(response);
	}
	catch (Exception ex) { return Results.Problem(title: "通知列表查询失败。", detail: ex.Message, statusCode: StatusCodes.Status502BadGateway); }
}).RequireAuthorization();

app.MapGet("/api/admin/notifications/observability", async (HttpContext context, IHttpClientFactory httpClientFactory, IConfiguration configuration, CancellationToken cancellationToken) =>
{
	try
	{
		var client = httpClientFactory.CreateClient();
		var response = await GetJsonAsync<NotificationObservabilityResponse>(client, context, $"{ResolveServiceUrl(configuration, "Notification", "http://localhost:5144")}/api/notifications/observability", cancellationToken);
		return Results.Ok(response);
	}
	catch (Exception ex) { return Results.Problem(title: "通知可观测数据查询失败。", detail: ex.Message, statusCode: StatusCodes.Status502BadGateway); }
}).RequireAuthorization();

// ── Operations / Alert Service Proxy ──────────────────────────────────────

app.MapGet("/api/admin/alerts/summary", async (HttpContext context, IHttpClientFactory httpClientFactory, IConfiguration configuration, CancellationToken cancellationToken) =>
{
	try
	{
		var client = httpClientFactory.CreateClient();
		var response = await GetJsonAsync<AdminAlertSummaryResponse>(client, context, $"{ResolveServiceUrl(configuration, "Operations", "http://localhost:5211")}/api/operations/alerts/summary", cancellationToken);
		return Results.Ok(response);
	}
	catch (Exception ex) { return Results.Problem(title: "报警 summary 查询失败。", detail: ex.Message, statusCode: StatusCodes.Status502BadGateway); }
}).RequireAuthorization();

app.MapGet("/api/admin/alerts", async (HttpContext context, IHttpClientFactory httpClientFactory, IConfiguration configuration, CancellationToken cancellationToken, string? module, string? level, string? status) =>
{
	try
	{
		var client = httpClientFactory.CreateClient();
		var query = new List<string>();
		if (!string.IsNullOrWhiteSpace(module)) query.Add($"module={Uri.EscapeDataString(module)}");
		if (!string.IsNullOrWhiteSpace(level)) query.Add($"level={Uri.EscapeDataString(level)}");
		if (!string.IsNullOrWhiteSpace(status)) query.Add($"status={Uri.EscapeDataString(status)}");
		var qs = query.Count > 0 ? $"?{string.Join("&", query)}" : string.Empty;
		var response = await GetJsonAsync<List<AdminAlertQueueItemResponse>>(client, context, $"{ResolveServiceUrl(configuration, "Operations", "http://localhost:5211")}/api/operations/alerts{qs}", cancellationToken);
		return Results.Ok(response);
	}
	catch (Exception ex) { return Results.Problem(title: "报警队列查询失败。", detail: ex.Message, statusCode: StatusCodes.Status502BadGateway); }
}).RequireAuthorization();

app.MapPost("/api/admin/alerts/{alertId}/actions", async (string alertId, AdminAlertActionRequest request, HttpContext context, IHttpClientFactory httpClientFactory, IConfiguration configuration, CancellationToken cancellationToken) =>
{
	try
	{
		var client = httpClientFactory.CreateClient();
		var response = await PostJsonAsync<AdminAlertQueueItemResponse>(client, context, $"{ResolveServiceUrl(configuration, "Operations", "http://localhost:5211")}/api/operations/alerts/{Uri.EscapeDataString(alertId)}/actions", request, cancellationToken);
		return Results.Ok(response);
	}
	catch (Exception ex) { return Results.Problem(title: "报警动作提交失败。", detail: ex.Message, statusCode: StatusCodes.Status502BadGateway); }
}).RequireAuthorization();

app.MapGet("/api/admin/activities", async (HttpContext context, IHttpClientFactory httpClientFactory, IConfiguration configuration, CancellationToken cancellationToken, string? keyword, string? status, string? lifecycleStatus, int page = 1, int pageSize = 20) =>
{
	try
	{
		var client = httpClientFactory.CreateClient();
		var query = new List<string>();
		if (!string.IsNullOrWhiteSpace(keyword)) query.Add($"keyword={Uri.EscapeDataString(keyword)}");
		if (!string.IsNullOrWhiteSpace(status)) query.Add($"status={Uri.EscapeDataString(status)}");
		if (!string.IsNullOrWhiteSpace(lifecycleStatus)) query.Add($"lifecycleStatus={Uri.EscapeDataString(lifecycleStatus)}");
		query.Add($"page={page}");
		query.Add($"pageSize={pageSize}");
		var qs = $"?{string.Join("&", query)}";
		var response = await GetJsonAsync<AdminActivityListResponse>(client, context, $"{ResolveServiceUrl(configuration, "Operations", "http://localhost:5211")}/api/operations/activities{qs}", cancellationToken);
		return Results.Ok(response);
	}
	catch (Exception ex) { return Results.Problem(title: "活动列表查询失败。", detail: ex.Message, statusCode: StatusCodes.Status502BadGateway); }
}).RequireAuthorization();

app.MapGet("/api/admin/activities/{activityId}", async (string activityId, HttpContext context, IHttpClientFactory httpClientFactory, IConfiguration configuration, CancellationToken cancellationToken) =>
{
	try
	{
		var client = httpClientFactory.CreateClient();
		var response = await GetJsonAsync<AdminActivityRecordResponse>(client, context, $"{ResolveServiceUrl(configuration, "Operations", "http://localhost:5211")}/api/operations/activities/{Uri.EscapeDataString(activityId)}", cancellationToken);
		return Results.Ok(response);
	}
	catch (Exception ex) { return Results.Problem(title: "活动详情查询失败。", detail: ex.Message, statusCode: StatusCodes.Status502BadGateway); }
}).RequireAuthorization();

app.MapPost("/api/admin/activities", async (AdminActivityCreateRequest request, HttpContext context, IHttpClientFactory httpClientFactory, IConfiguration configuration, CancellationToken cancellationToken) =>
{
	try
	{
		var client = httpClientFactory.CreateClient();
		var response = await PostJsonAsync<AdminActivityRecordResponse>(client, context, $"{ResolveServiceUrl(configuration, "Operations", "http://localhost:5211")}/api/operations/activities", request, cancellationToken);
		return Results.Ok(response);
	}
	catch (Exception ex) { return Results.Problem(title: "活动创建失败。", detail: ex.Message, statusCode: StatusCodes.Status502BadGateway); }
}).RequireAuthorization();

app.MapPost("/api/admin/activities/{activityId}/actions", async (string activityId, AdminActivityActionRequest request, HttpContext context, IHttpClientFactory httpClientFactory, IConfiguration configuration, CancellationToken cancellationToken) =>
{
	try
	{
		var client = httpClientFactory.CreateClient();
		var response = await PostJsonAsync<AdminActivityRecordResponse>(client, context, $"{ResolveServiceUrl(configuration, "Operations", "http://localhost:5211")}/api/operations/activities/{Uri.EscapeDataString(activityId)}/actions", request, cancellationToken);
		return Results.Ok(response);
	}
	catch (Exception ex) { return Results.Problem(title: "活动动作提交失败。", detail: ex.Message, statusCode: StatusCodes.Status502BadGateway); }
}).RequireAuthorization();

app.MapGet("/api/admin/incidents", async (HttpContext context, IHttpClientFactory httpClientFactory, IConfiguration configuration, CancellationToken cancellationToken, string? keyword, string? level, string? status, int page = 1, int pageSize = 20) =>
{
	try
	{
		var client = httpClientFactory.CreateClient();
		var query = new List<string>();
		if (!string.IsNullOrWhiteSpace(keyword)) query.Add($"keyword={Uri.EscapeDataString(keyword)}");
		if (!string.IsNullOrWhiteSpace(level)) query.Add($"level={Uri.EscapeDataString(level)}");
		if (!string.IsNullOrWhiteSpace(status)) query.Add($"status={Uri.EscapeDataString(status)}");
		query.Add($"page={page}");
		query.Add($"pageSize={pageSize}");
		var qs = $"?{string.Join("&", query)}";
		var response = await GetJsonAsync<AdminIncidentListResponse>(client, context, $"{ResolveServiceUrl(configuration, "Operations", "http://localhost:5211")}/api/operations/incidents{qs}", cancellationToken);
		return Results.Ok(response);
	}
	catch (Exception ex) { return Results.Problem(title: "事故列表查询失败。", detail: ex.Message, statusCode: StatusCodes.Status502BadGateway); }
}).RequireAuthorization();

app.MapGet("/api/admin/incidents/{incidentId}", async (string incidentId, HttpContext context, IHttpClientFactory httpClientFactory, IConfiguration configuration, CancellationToken cancellationToken) =>
{
	try
	{
		var client = httpClientFactory.CreateClient();
		var response = await GetJsonAsync<AdminIncidentRecordResponse>(client, context, $"{ResolveServiceUrl(configuration, "Operations", "http://localhost:5211")}/api/operations/incidents/{Uri.EscapeDataString(incidentId)}", cancellationToken);
		return Results.Ok(response);
	}
	catch (Exception ex) { return Results.Problem(title: "事故详情查询失败。", detail: ex.Message, statusCode: StatusCodes.Status502BadGateway); }
}).RequireAuthorization();

app.MapPost("/api/admin/incidents", async (AdminIncidentCreateRequest request, HttpContext context, IHttpClientFactory httpClientFactory, IConfiguration configuration, CancellationToken cancellationToken) =>
{
	try
	{
		var client = httpClientFactory.CreateClient();
		var response = await PostJsonAsync<AdminIncidentRecordResponse>(client, context, $"{ResolveServiceUrl(configuration, "Operations", "http://localhost:5211")}/api/operations/incidents", request, cancellationToken);
		return Results.Ok(response);
	}
	catch (Exception ex) { return Results.Problem(title: "事故创建失败。", detail: ex.Message, statusCode: StatusCodes.Status502BadGateway); }
}).RequireAuthorization();

app.MapPost("/api/admin/incidents/{incidentId}/actions", async (string incidentId, AdminIncidentActionRequest request, HttpContext context, IHttpClientFactory httpClientFactory, IConfiguration configuration, CancellationToken cancellationToken) =>
{
	try
	{
		var client = httpClientFactory.CreateClient();
		var response = await PostJsonAsync<AdminIncidentRecordResponse>(client, context, $"{ResolveServiceUrl(configuration, "Operations", "http://localhost:5211")}/api/operations/incidents/{Uri.EscapeDataString(incidentId)}/actions", request, cancellationToken);
		return Results.Ok(response);
	}
	catch (Exception ex) { return Results.Problem(title: "事故动作提交失败。", detail: ex.Message, statusCode: StatusCodes.Status502BadGateway); }
}).RequireAuthorization();

app.MapGet("/api/admin/equipment", async (HttpContext context, IHttpClientFactory httpClientFactory, IConfiguration configuration, CancellationToken cancellationToken, string? keyword, string? category, string? status, string? lifecycleStatus, int page = 1, int pageSize = 20) =>
{
	try
	{
		var client = httpClientFactory.CreateClient();
		var query = new List<string>();
		if (!string.IsNullOrWhiteSpace(keyword)) query.Add($"keyword={Uri.EscapeDataString(keyword)}");
		if (!string.IsNullOrWhiteSpace(category)) query.Add($"category={Uri.EscapeDataString(category)}");
		if (!string.IsNullOrWhiteSpace(status)) query.Add($"status={Uri.EscapeDataString(status)}");
		if (!string.IsNullOrWhiteSpace(lifecycleStatus)) query.Add($"lifecycleStatus={Uri.EscapeDataString(lifecycleStatus)}");
		query.Add($"page={page}");
		query.Add($"pageSize={pageSize}");
		var qs = $"?{string.Join("&", query)}";
		var response = await GetJsonAsync<AdminEquipmentListResponse>(client, context, $"{ResolveServiceUrl(configuration, "Operations", "http://localhost:5211")}/api/operations/equipment{qs}", cancellationToken);
		return Results.Ok(response);
	}
	catch (Exception ex) { return Results.Problem(title: "设备列表查询失败。", detail: ex.Message, statusCode: StatusCodes.Status502BadGateway); }
}).RequireAuthorization();

app.MapGet("/api/admin/equipment/{equipmentId}", async (string equipmentId, HttpContext context, IHttpClientFactory httpClientFactory, IConfiguration configuration, CancellationToken cancellationToken) =>
{
	try
	{
		var client = httpClientFactory.CreateClient();
		var response = await GetJsonAsync<AdminEquipmentRecordResponse>(client, context, $"{ResolveServiceUrl(configuration, "Operations", "http://localhost:5211")}/api/operations/equipment/{Uri.EscapeDataString(equipmentId)}", cancellationToken);
		return Results.Ok(response);
	}
	catch (Exception ex) { return Results.Problem(title: "设备详情查询失败。", detail: ex.Message, statusCode: StatusCodes.Status502BadGateway); }
}).RequireAuthorization();

app.MapPost("/api/admin/equipment", async (AdminEquipmentCreateRequest request, HttpContext context, IHttpClientFactory httpClientFactory, IConfiguration configuration, CancellationToken cancellationToken) =>
{
	try
	{
		var client = httpClientFactory.CreateClient();
		var response = await PostJsonAsync<AdminEquipmentRecordResponse>(client, context, $"{ResolveServiceUrl(configuration, "Operations", "http://localhost:5211")}/api/operations/equipment", request, cancellationToken);
		return Results.Ok(response);
	}
	catch (Exception ex) { return Results.Problem(title: "设备创建失败。", detail: ex.Message, statusCode: StatusCodes.Status502BadGateway); }
}).RequireAuthorization();

app.MapPost("/api/admin/equipment/{equipmentId}/activate", async (string equipmentId, AdminEquipmentActivateRequest request, HttpContext context, IHttpClientFactory httpClientFactory, IConfiguration configuration, CancellationToken cancellationToken) =>
{
	try
	{
		var client = httpClientFactory.CreateClient();
		var response = await PostJsonAsync<AdminEquipmentRecordResponse>(client, context, $"{ResolveServiceUrl(configuration, "Operations", "http://localhost:5211")}/api/operations/equipment/{Uri.EscapeDataString(equipmentId)}/activate", request, cancellationToken);
		return Results.Ok(response);
	}
	catch (Exception ex) { return Results.Problem(title: "设备验收失败。", detail: ex.Message, statusCode: StatusCodes.Status502BadGateway); }
}).RequireAuthorization();

app.MapGet("/api/admin/supplies", async (HttpContext context, IHttpClientFactory httpClientFactory, IConfiguration configuration, CancellationToken cancellationToken, string? keyword, string? category, string? status, string? lifecycleStatus, int page = 1, int pageSize = 20) =>
{
	try
	{
		var client = httpClientFactory.CreateClient();
		var query = new List<string>();
		if (!string.IsNullOrWhiteSpace(keyword)) query.Add($"keyword={Uri.EscapeDataString(keyword)}");
		if (!string.IsNullOrWhiteSpace(category)) query.Add($"category={Uri.EscapeDataString(category)}");
		if (!string.IsNullOrWhiteSpace(status)) query.Add($"status={Uri.EscapeDataString(status)}");
		if (!string.IsNullOrWhiteSpace(lifecycleStatus)) query.Add($"lifecycleStatus={Uri.EscapeDataString(lifecycleStatus)}");
		query.Add($"page={page}");
		query.Add($"pageSize={pageSize}");
		var qs = $"?{string.Join("&", query)}";
		var response = await GetJsonAsync<AdminSupplyListResponse>(client, context, $"{ResolveServiceUrl(configuration, "Operations", "http://localhost:5211")}/api/operations/supplies{qs}", cancellationToken);
		return Results.Ok(response);
	}
	catch (Exception ex) { return Results.Problem(title: "物资列表查询失败。", detail: ex.Message, statusCode: StatusCodes.Status502BadGateway); }
}).RequireAuthorization();

app.MapGet("/api/admin/supplies/{supplyId}", async (string supplyId, HttpContext context, IHttpClientFactory httpClientFactory, IConfiguration configuration, CancellationToken cancellationToken) =>
{
	try
	{
		var client = httpClientFactory.CreateClient();
		var response = await GetJsonAsync<AdminSupplyRecordResponse>(client, context, $"{ResolveServiceUrl(configuration, "Operations", "http://localhost:5211")}/api/operations/supplies/{Uri.EscapeDataString(supplyId)}", cancellationToken);
		return Results.Ok(response);
	}
	catch (Exception ex) { return Results.Problem(title: "物资详情查询失败。", detail: ex.Message, statusCode: StatusCodes.Status502BadGateway); }
}).RequireAuthorization();

app.MapPost("/api/admin/supplies", async (AdminSupplyIntakeRequest request, HttpContext context, IHttpClientFactory httpClientFactory, IConfiguration configuration, CancellationToken cancellationToken) =>
{
	try
	{
		var client = httpClientFactory.CreateClient();
		var response = await PostJsonAsync<AdminSupplyRecordResponse>(client, context, $"{ResolveServiceUrl(configuration, "Operations", "http://localhost:5211")}/api/operations/supplies", request, cancellationToken);
		return Results.Ok(response);
	}
	catch (Exception ex) { return Results.Problem(title: "物资入库失败。", detail: ex.Message, statusCode: StatusCodes.Status502BadGateway); }
}).RequireAuthorization();

app.MapPost("/api/admin/supplies/{supplyId}/activate", async (string supplyId, AdminSupplyActivateRequest request, HttpContext context, IHttpClientFactory httpClientFactory, IConfiguration configuration, CancellationToken cancellationToken) =>
{
	try
	{
		var client = httpClientFactory.CreateClient();
		var response = await PostJsonAsync<AdminSupplyRecordResponse>(client, context, $"{ResolveServiceUrl(configuration, "Operations", "http://localhost:5211")}/api/operations/supplies/{Uri.EscapeDataString(supplyId)}/activate", request, cancellationToken);
		return Results.Ok(response);
	}
	catch (Exception ex) { return Results.Problem(title: "物资上架确认失败。", detail: ex.Message, statusCode: StatusCodes.Status502BadGateway); }
}).RequireAuthorization();

// ── Tenant Service Proxy ───────────────────────────────────────────────────

app.MapGet("/api/admin/roles", async (HttpContext context, IHttpClientFactory httpClientFactory, IConfiguration configuration, CancellationToken cancellationToken) =>
{
	try
	{
		var client = httpClientFactory.CreateClient();
		var response = await GetJsonAsync<List<AdminRoleDescriptorResponse>>(
			client,
			context,
			$"{ResolveServiceUrl(configuration, "Identity", "http://localhost:5180")}/api/identity/roles",
			cancellationToken);
		return Results.Ok(response);
	}
	catch (Exception ex) { return Results.Problem(title: "角色列表查询失败。", detail: ex.Message, statusCode: StatusCodes.Status502BadGateway); }
}).RequireAuthorization();

app.MapGet("/api/admin/tenants", async (HttpContext context, IHttpClientFactory httpClientFactory, IConfiguration configuration, CancellationToken cancellationToken) =>
{
	try
	{
		var client = httpClientFactory.CreateClient();
		var response = await GetJsonAsync<List<TenantDescriptorResponse>>(client, context, $"{ResolveServiceUrl(configuration, "Tenant", "http://localhost:5186")}/api/tenants", cancellationToken);
		return Results.Ok(response);
	}
	catch (Exception ex) { return Results.Problem(title: "租户列表查询失败。", detail: ex.Message, statusCode: StatusCodes.Status502BadGateway); }
}).RequireAuthorization();

app.MapGet("/api/admin/tenants/{tenantId}", async (string tenantId, HttpContext context, IHttpClientFactory httpClientFactory, IConfiguration configuration, CancellationToken cancellationToken) =>
{
	try
	{
		var client = httpClientFactory.CreateClient();
		var response = await GetJsonAsync<TenantDescriptorResponse>(client, context, $"{ResolveServiceUrl(configuration, "Tenant", "http://localhost:5186")}/api/tenants/{Uri.EscapeDataString(tenantId)}", cancellationToken);
		return Results.Ok(response);
	}
	catch (Exception ex) { return Results.Problem(title: "租户详情查询失败。", detail: ex.Message, statusCode: StatusCodes.Status502BadGateway); }
}).RequireAuthorization();

app.MapGet("/api/admin/dashboard/overview", async (HttpContext context, IHttpClientFactory httpClientFactory, IConfiguration configuration, CancellationToken cancellationToken) =>
{
	try
	{
		var client = httpClientFactory.CreateClient();

		var elderTask = GetJsonAsync<ElderListResponse>(
			client,
			context,
			$"{ResolveServiceUrl(configuration, "Elder", "http://localhost:5062")}/api/elders?page=1&pageSize=1",
			cancellationToken);

		var tenantTask = GetJsonAsync<List<TenantDescriptorResponse>>(
			client,
			context,
			$"{ResolveServiceUrl(configuration, "Tenant", "http://localhost:5186")}/api/tenants",
			cancellationToken);

		var financeTask = GetJsonAsync<AdminFinanceSummaryResponse>(
			client,
			context,
			$"{ResolveServiceUrl(configuration, "Billing", "http://localhost:5253")}/api/billing/summary",
			cancellationToken);

		var notificationTask = GetJsonAsync<AdminNotificationSummaryResponse>(
			client,
			context,
			$"{ResolveServiceUrl(configuration, "Notification", "http://localhost:5144")}/api/notifications/summary",
			cancellationToken);

		var alertTask = GetJsonAsync<AdminAlertSummaryResponse>(
			client,
			context,
			$"{ResolveServiceUrl(configuration, "Operations", "http://localhost:5211")}/api/operations/alerts/summary",
			cancellationToken);

		var workflowTask = GetJsonAsync<NursingWorkflowBoardResponse>(
			client,
			context,
			$"{ResolveServiceUrl(configuration, "Care", "http://localhost:5019")}/api/care/admin/workflow-board",
			cancellationToken);

		await Task.WhenAll(elderTask, tenantTask, financeTask, notificationTask, alertTask, workflowTask);

		var elderResponse = await elderTask;
		var tenantResponse = await tenantTask;
		var financeResponse = await financeTask;
		var notificationResponse = await notificationTask;
		var alertResponse = await alertTask;
		var workflowResponse = await workflowTask;

		var pendingAlerts = alertResponse.Modules.Sum(module => module.Pending + module.Processing);
		var workflowPendingCount = workflowResponse.Schedule.PendingReviewPlans + workflowResponse.Schedule.UnassignedPlans;
		var alertModules = alertResponse.Modules
			.Select(module => new AdminDashboardAlertModuleBreakdownResponse(
				Label: module.Module,
				Pending: module.Pending,
				Processing: module.Processing,
				Resolved: module.Resolved,
				Critical: module.Critical,
				TotalOpen: module.Pending + module.Processing))
			.OrderByDescending(module => module.TotalOpen)
			.ThenBy(module => module.Label)
			.ToArray();

		var notificationBreakdown = new[]
		{
			new AdminDashboardMetricItemResponse("待发送", notificationResponse.Queued),
			new AdminDashboardMetricItemResponse("已送达", notificationResponse.Delivered),
			new AdminDashboardMetricItemResponse("发送失败", notificationResponse.Failed),
			new AdminDashboardMetricItemResponse("广播通知", notificationResponse.Broadcasts),
			new AdminDashboardMetricItemResponse("定时提醒", notificationResponse.ScheduledReminders),
		};

		var financeBreakdown = new[]
		{
			new AdminDashboardMetricItemResponse("待复核", financeResponse.PendingReview),
			new AdminDashboardMetricItemResponse("已开票", financeResponse.Issued),
			new AdminDashboardMetricItemResponse("已逾期", financeResponse.Overdue),
			new AdminDashboardMetricItemResponse("待归档", financeResponse.PendingArchive),
			new AdminDashboardMetricItemResponse("动作必做", financeResponse.ActionRequired),
		};

		var workflowBreakdown = new[]
		{
			new AdminDashboardMetricItemResponse("活跃计划", workflowResponse.Schedule.ActivePlans),
			new AdminDashboardMetricItemResponse("待复核计划", workflowResponse.Schedule.PendingReviewPlans),
			new AdminDashboardMetricItemResponse("未分配计划", workflowResponse.Schedule.UnassignedPlans),
			new AdminDashboardMetricItemResponse("已发布排班", workflowResponse.Schedule.PublishedAssignments),
			new AdminDashboardMetricItemResponse("已完成任务", workflowResponse.Observability.CompletedTasks),
		};

		var staffLeaderboard = workflowResponse.Tasks
			.Where(task => !string.IsNullOrWhiteSpace(task.OwnerName))
			.GroupBy(task => new { task.OwnerName, task.OwnerRole })
			.Select(group =>
			{
				var tasks = group.Count();
				var completed = group.Count(task => task.Status == "已完成");
				var completionRate = tasks == 0 ? 0 : (int)Math.Round(completed * 100d / tasks, MidpointRounding.AwayFromZero);
				return new AdminDashboardStaffLeaderboardItemResponse(
					Name: group.Key.OwnerName,
					Role: group.Key.OwnerRole,
					Tasks: tasks,
					Completed: completed,
					CompletionRate: completionRate,
					Trend: completionRate >= 95 ? "up" : "down");
			})
			.OrderByDescending(item => item.CompletionRate)
			.ThenByDescending(item => item.Completed)
			.ThenBy(item => item.Name)
			.Take(7)
			.ToArray();

		return Results.Ok(new AdminDashboardOverviewResponse(
			GeneratedAtUtc: DateTimeOffset.UtcNow,
			Kpis: new AdminDashboardKpiResponse(
				ElderCount: elderResponse.Total,
				TenantCount: tenantResponse.Count,
				PendingAlerts: pendingAlerts,
				WorkflowPendingCount: workflowPendingCount),
			AlertModules: alertModules,
			NotificationBreakdown: notificationBreakdown,
			FinanceBreakdown: financeBreakdown,
			WorkflowBreakdown: workflowBreakdown,
			StaffLeaderboard: staffLeaderboard));
	}
	catch (Exception ex) { return Results.Problem(title: "Dashboard 聚合查询失败。", detail: ex.Message, statusCode: StatusCodes.Status502BadGateway); }
}).RequireAuthorization();

// ── AI Orchestration Proxy ─────────────────────────────────────────────────

app.MapPost("/api/admin/ai/dashboard-insights", async (AiDashboardInsightsRequest request, HttpContext context, IHttpClientFactory httpClientFactory, IConfiguration configuration, CancellationToken ct) =>
{
	try
	{
		var client = httpClientFactory.CreateClient();
		var response = await PostJsonAsync<object>(client, context, $"{ResolveServiceUrl(configuration, "AiOrchestration", "http://localhost:5267")}/api/ai/dashboard-insights", request, ct);
		return Results.Ok(response);
	}
	catch (Exception ex) { return Results.Problem(title: "AI Dashboard 分析失败。", detail: ex.Message, statusCode: StatusCodes.Status502BadGateway); }
}).RequireAuthorization();

app.MapPost("/api/admin/ai/health-risk", async (AiHealthRiskRequest request, HttpContext context, IHttpClientFactory httpClientFactory, IConfiguration configuration, CancellationToken ct) =>
{
	try
	{
		var client = httpClientFactory.CreateClient();
		var response = await PostJsonAsync<object>(client, context, $"{ResolveServiceUrl(configuration, "AiOrchestration", "http://localhost:5267")}/api/ai/health-risk", request, ct);
		return Results.Ok(response);
	}
	catch (Exception ex) { return Results.Problem(title: "AI 健康风险分析失败。", detail: ex.Message, statusCode: StatusCodes.Status502BadGateway); }
}).RequireAuthorization();

app.MapPost("/api/admin/ai/alert-suggestion", async (AiAlertSuggestionRequest request, HttpContext context, IHttpClientFactory httpClientFactory, IConfiguration configuration, CancellationToken ct) =>
{
	try
	{
		var client = httpClientFactory.CreateClient();
		var response = await PostJsonAsync<object>(client, context, $"{ResolveServiceUrl(configuration, "AiOrchestration", "http://localhost:5267")}/api/ai/alert-suggestion", request, ct);
		return Results.Ok(response);
	}
	catch (Exception ex) { return Results.Problem(title: "AI 报警建议失败。", detail: ex.Message, statusCode: StatusCodes.Status502BadGateway); }
}).RequireAuthorization();

app.MapPost("/api/admin/ai/task-priority", async (AiTaskPriorityRequest request, HttpContext context, IHttpClientFactory httpClientFactory, IConfiguration configuration, CancellationToken ct) =>
{
	try
	{
		var client = httpClientFactory.CreateClient();
		var response = await PostJsonAsync<object>(client, context, $"{ResolveServiceUrl(configuration, "AiOrchestration", "http://localhost:5267")}/api/ai/task-priority", request, ct);
		return Results.Ok(response);
	}
	catch (Exception ex) { return Results.Problem(title: "AI 任务优先级排序失败。", detail: ex.Message, statusCode: StatusCodes.Status502BadGateway); }
}).RequireAuthorization();

app.MapPost("/api/admin/ai/admission-assessment", async (AiAdmissionAssessmentRequest request, HttpContext context, IHttpClientFactory httpClientFactory, IConfiguration configuration, CancellationToken ct) =>
{
	try
	{
		var client = httpClientFactory.CreateClient();
		var response = await PostJsonAsync<object>(client, context, $"{ResolveServiceUrl(configuration, "AiOrchestration", "http://localhost:5267")}/api/ai/admission-assessment", request, ct);
		return Results.Ok(response);
	}
	catch (Exception ex) { return Results.Problem(title: "AI 入住评估失败。", detail: ex.Message, statusCode: StatusCodes.Status502BadGateway); }
}).RequireAuthorization();

app.MapPost("/api/admin/ai/ops-report", async (AiOpsReportRequest request, HttpContext context, IHttpClientFactory httpClientFactory, IConfiguration configuration, CancellationToken ct) =>
{
	try
	{
		var client = httpClientFactory.CreateClient();
		var response = await PostJsonAsync<object>(client, context, $"{ResolveServiceUrl(configuration, "AiOrchestration", "http://localhost:5267")}/api/ai/ops-report", request, ct);
		return Results.Ok(response);
	}
	catch (Exception ex) { return Results.Problem(title: "AI 运营报告生成失败。", detail: ex.Message, statusCode: StatusCodes.Status502BadGateway); }
}).RequireAuthorization();

app.MapPost("/api/admin/ai/financial-insights", async (AiResourceInsightsRequest request, HttpContext context, IHttpClientFactory httpClientFactory, IConfiguration configuration, CancellationToken ct) =>
{
	try
	{
		var client = httpClientFactory.CreateClient();
		var response = await PostJsonAsync<object>(client, context, $"{ResolveServiceUrl(configuration, "AiOrchestration", "http://localhost:5267")}/api/ai/financial-insights", request, ct);
		return Results.Ok(response);
	}
	catch (Exception ex) { return Results.Problem(title: "AI 财务分析失败。", detail: ex.Message, statusCode: StatusCodes.Status502BadGateway); }
}).RequireAuthorization();

app.MapPost("/api/admin/ai/device-insights", async (AiAlertSuggestionRequest request, HttpContext context, IHttpClientFactory httpClientFactory, IConfiguration configuration, CancellationToken ct) =>
{
	try
	{
		var client = httpClientFactory.CreateClient();
		var response = await PostJsonAsync<object>(client, context, $"{ResolveServiceUrl(configuration, "AiOrchestration", "http://localhost:5267")}/api/ai/device-insights", request, ct);
		return Results.Ok(response);
	}
	catch (Exception ex) { return Results.Problem(title: "AI 设备分析失败。", detail: ex.Message, statusCode: StatusCodes.Status502BadGateway); }
}).RequireAuthorization();

app.MapPost("/api/admin/ai/incident-analysis", async (AiAlertSuggestionRequest request, HttpContext context, IHttpClientFactory httpClientFactory, IConfiguration configuration, CancellationToken ct) =>
{
	try
	{
		var client = httpClientFactory.CreateClient();
		var response = await PostJsonAsync<object>(client, context, $"{ResolveServiceUrl(configuration, "AiOrchestration", "http://localhost:5267")}/api/ai/incident-analysis", request, ct);
		return Results.Ok(response);
	}
	catch (Exception ex) { return Results.Problem(title: "AI 事故分析失败。", detail: ex.Message, statusCode: StatusCodes.Status502BadGateway); }
}).RequireAuthorization();

app.MapPost("/api/admin/ai/resource-insights", async (AiResourceInsightsRequest request, HttpContext context, IHttpClientFactory httpClientFactory, IConfiguration configuration, CancellationToken ct) =>
{
	try
	{
		var client = httpClientFactory.CreateClient();
		var response = await PostJsonAsync<object>(client, context, $"{ResolveServiceUrl(configuration, "AiOrchestration", "http://localhost:5267")}/api/ai/resource-insights", request, ct);
		return Results.Ok(response);
	}
	catch (Exception ex) { return Results.Problem(title: "AI 资源分析失败。", detail: ex.Message, statusCode: StatusCodes.Status502BadGateway); }
}).RequireAuthorization();

app.MapPost("/api/admin/ai/chat", async (AiChatRequest request, HttpContext context, IHttpClientFactory httpClientFactory, IConfiguration configuration, CancellationToken ct) =>
{
	try
	{
		var client = httpClientFactory.CreateClient();
		var response = await PostJsonAsync<object>(client, context, $"{ResolveServiceUrl(configuration, "AiOrchestration", "http://localhost:5267")}/api/ai/chat", request, ct);
		return Results.Ok(response);
	}
	catch (Exception ex) { return Results.Problem(title: "AI 问答失败。", detail: ex.Message, statusCode: StatusCodes.Status502BadGateway); }
}).RequireAuthorization();

app.MapPost("/api/admin/ai/elder-detail-action", async (AiAlertSuggestionRequest request, HttpContext context, IHttpClientFactory httpClientFactory, IConfiguration configuration, CancellationToken ct) =>
{
	try
	{
		var client = httpClientFactory.CreateClient();
		var response = await PostJsonAsync<object>(client, context, $"{ResolveServiceUrl(configuration, "AiOrchestration", "http://localhost:5267")}/api/ai/elder-detail-action", request, ct);
		return Results.Ok(response);
	}
	catch (Exception ex) { return Results.Problem(title: "AI 老人详情分析失败。", detail: ex.Message, statusCode: StatusCodes.Status502BadGateway); }
}).RequireAuthorization();

app.MapGet("/api/admin/ai/rules", async (HttpContext context, IHttpClientFactory httpClientFactory, IConfiguration configuration, CancellationToken ct) =>
{
	try
	{
		var client = httpClientFactory.CreateClient();
		var response = await GetJsonAsync<object>(client, context, $"{ResolveServiceUrl(configuration, "AiOrchestration", "http://localhost:5267")}/api/ai/rules", ct);
		return Results.Ok(response);
	}
	catch (Exception ex) { return Results.Problem(title: "AI 规则查询失败。", detail: ex.Message, statusCode: StatusCodes.Status502BadGateway); }
}).RequireAuthorization();

app.MapPatch("/api/admin/ai/rules/{ruleId}/toggle", async (string ruleId, AiRuleToggleRequest request, HttpContext context, IHttpClientFactory httpClientFactory, IConfiguration configuration, CancellationToken ct) =>
{
	try
	{
		var client = httpClientFactory.CreateClient();
		using var req = DownstreamHttp.CreateJsonRequest(HttpMethod.Patch, $"{ResolveServiceUrl(configuration, "AiOrchestration", "http://localhost:5267")}/api/ai/rules/{ruleId}/toggle", context, request);
		using var resp = await client.SendAsync(req, ct);
		resp.EnsureSuccessStatusCode();
		return Results.Ok(await resp.ReadJsonAsync<object>(ct));
	}
	catch (Exception ex) { return Results.Problem(title: "AI 规则切换失败。", detail: ex.Message, statusCode: StatusCodes.Status502BadGateway); }
}).RequireAuthorization();

app.MapGet("/api/admin/ai/models/status", async (HttpContext context, IHttpClientFactory httpClientFactory, IConfiguration configuration, CancellationToken ct) =>
{
	try
	{
		var client = httpClientFactory.CreateClient();
		var response = await GetJsonAsync<object>(client, context, $"{ResolveServiceUrl(configuration, "AiOrchestration", "http://localhost:5267")}/api/ai/models/status", ct);
		return Results.Ok(response);
	}
	catch (Exception ex) { return Results.Problem(title: "AI 模型状态查询失败。", detail: ex.Message, statusCode: StatusCodes.Status502BadGateway); }
}).RequireAuthorization();

app.MapGet("/api/admin/ai/audit-logs", async (HttpContext context, IHttpClientFactory httpClientFactory, IConfiguration configuration, CancellationToken ct, string? capability, int page = 1, int pageSize = 20) =>
{
	try
	{
		var client = httpClientFactory.CreateClient();
		var qs = $"?page={page}&pageSize={pageSize}";
		if (!string.IsNullOrWhiteSpace(capability)) qs += $"&capability={Uri.EscapeDataString(capability)}";
		var response = await GetJsonAsync<object>(client, context, $"{ResolveServiceUrl(configuration, "AiOrchestration", "http://localhost:5267")}/api/ai/audit-logs{qs}", ct);
		return Results.Ok(response);
	}
	catch (Exception ex) { return Results.Problem(title: "AI 审计日志查询失败。", detail: ex.Message, statusCode: StatusCodes.Status502BadGateway); }
}).RequireAuthorization();

app.MapGet("/api/admin/ai/audit-logs/{auditId}", async (string auditId, HttpContext context, IHttpClientFactory httpClientFactory, IConfiguration configuration, CancellationToken ct) =>
{
	try
	{
		var client = httpClientFactory.CreateClient();
		var response = await GetJsonAsync<object>(client, context, $"{ResolveServiceUrl(configuration, "AiOrchestration", "http://localhost:5267")}/api/ai/audit-logs/{Uri.EscapeDataString(auditId)}", ct);
		return Results.Ok(response);
	}
	catch (Exception ex) { return Results.Problem(title: "AI 审计详情查询失败。", detail: ex.Message, statusCode: StatusCodes.Status502BadGateway); }
}).RequireAuthorization();

app.Run();

static string ResolveServiceUrl(IConfiguration configuration, string serviceName, string fallback)
{
	return configuration[$"ServiceEndpoints:{serviceName}"] ?? fallback;
}

static async Task<T> GetJsonAsync<T>(HttpClient client, HttpContext context, string url, CancellationToken cancellationToken)
{
	using var request = DownstreamHttp.CreateJsonRequest(HttpMethod.Get, url, context);
	using var response = await client.SendAsync(request, cancellationToken);
	response.EnsureSuccessStatusCode();
	return (await response.ReadJsonAsync<T>(cancellationToken))!;
}

static async Task<T> PostJsonAsync<T>(HttpClient client, HttpContext context, string url, object payload, CancellationToken cancellationToken)
{
	using var request = DownstreamHttp.CreateJsonRequest(HttpMethod.Post, url, context, payload);
	using var response = await client.SendAsync(request, cancellationToken);
	response.EnsureSuccessStatusCode();
	return (await response.ReadJsonAsync<T>(cancellationToken))!;
}

static async Task<T> PutJsonAsync<T>(HttpClient client, HttpContext context, string url, object payload, CancellationToken cancellationToken)
{
	using var request = DownstreamHttp.CreateJsonRequest(HttpMethod.Put, url, context, payload);
	using var response = await client.SendAsync(request, cancellationToken);
	response.EnsureSuccessStatusCode();
	return (await response.ReadJsonAsync<T>(cancellationToken))!;
}

static async Task<IReadOnlyList<ElderListItemResponse>> FetchAllEldersAsync(HttpClient client, HttpContext context, IConfiguration configuration, CancellationToken cancellationToken)
{
	const int pageSize = 200;
	var page = 1;
	var items = new List<ElderListItemResponse>();

	while (true)
	{
		var response = await GetJsonAsync<ElderListResponse>(
			client,
			context,
			$"{ResolveServiceUrl(configuration, "Elder", "http://localhost:5062")}/api/elders?status={Uri.EscapeDataString("Active")}&page={page}&pageSize={pageSize}",
			cancellationToken);

		items.AddRange(response.Items);
		if (items.Count >= response.Total || response.Items.Count == 0)
		{
			break;
		}

		page += 1;
	}

	return items;
}

static async Task<IReadOnlyList<AdminRoomRecordResponse>> FetchAllRoomsAsync(HttpClient client, HttpContext context, IConfiguration configuration, CancellationToken cancellationToken)
{
	const int pageSize = 200;
	var page = 1;
	var items = new List<AdminRoomRecordResponse>();

	while (true)
	{
		var response = await GetJsonAsync<AdminRoomListResponse>(
			client,
			context,
			$"{ResolveServiceUrl(configuration, "Rooms", "http://localhost:5217")}/api/rooms/rooms?page={page}&pageSize={pageSize}",
			cancellationToken);

		items.AddRange(response.Items);
		if (items.Count >= response.Total || response.Items.Count == 0)
		{
			break;
		}

		page += 1;
	}

	return items;
}

static async Task<IReadOnlyList<AdminStaffRecordResponse>> FetchAllStaffAsync(HttpClient client, HttpContext context, IConfiguration configuration, CancellationToken cancellationToken, string? organizationId = null)
{
	const int pageSize = 200;
	var page = 1;
	var items = new List<AdminStaffRecordResponse>();
	var organizationFilter = string.IsNullOrWhiteSpace(organizationId)
		? string.Empty
		: $"&organizationId={Uri.EscapeDataString(organizationId)}";

	while (true)
	{
		var response = await GetJsonAsync<AdminStaffListResponse>(
			client,
			context,
			$"{ResolveServiceUrl(configuration, "Staffing", "http://localhost:5216")}/api/staffing/staff?page={page}&pageSize={pageSize}{organizationFilter}",
			cancellationToken);

		items.AddRange(response.Items);
		if (items.Count >= response.Total || response.Items.Count == 0)
		{
			break;
		}

		page += 1;
	}

	return items;
}

static AdminRoomRecordResponse MergeRoomRecord(AdminRoomRecordResponse room, IReadOnlyList<ElderListItemResponse> elders)
	=> AdminBffAggregationPolicy.MergeRoomRecord(room, elders);

static bool MatchesOrganization(AdminRoomRecordResponse room, OrganizationRecordResponse organization)
	=> AdminBffAggregationPolicy.MatchesOrganization(room, organization);

static bool MatchesStaffOrganization(AdminStaffRecordResponse staff, OrganizationRecordResponse organization)
	=> AdminBffAggregationPolicy.MatchesStaffOrganization(staff, organization);

static AdminOrganizationSummaryResponse MergeOrganizationSummary(OrganizationRecordResponse organization, IReadOnlyList<AdminRoomRecordResponse> rooms, IReadOnlyList<AdminStaffRecordResponse> staff)
	=> AdminBffAggregationPolicy.MergeOrganizationSummary(organization, rooms, staff);

static AdminOrganizationRoomSummaryResponse MapOrganizationRoom(AdminRoomRecordResponse room) => AdminBffAggregationPolicy.MapOrganizationRoom(room);

static IReadOnlyList<string> BuildAssessmentMedicalAlerts(AdminAssessmentCaseCreateRequest request)
	=> AdminBffAggregationPolicy.BuildAssessmentMedicalAlerts(request);

static AssessmentAiRecommendationResponse BuildAssessmentAiRecommendation(AdminAssessmentCaseCreateRequest request, AiAdmissionAssessmentResponse response)
	=> AdminBffAggregationPolicy.BuildAssessmentAiRecommendation(request, response);
